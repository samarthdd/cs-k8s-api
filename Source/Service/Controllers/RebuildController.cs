using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTracing;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class RebuildController : CloudProxyController<RebuildController>
    {
        private readonly IAdaptationServiceClient<AdaptationOutcomeProcessor> _adaptationServiceClient;
        private readonly IFileUtility _fileUtility;
        private readonly IZipUtility _zipUtility;
        private readonly IStoreConfiguration _storeConfiguration;
        private readonly IProcessingConfiguration _processingConfiguration;
        private readonly CancellationTokenSource _processingCancellationTokenSource;
        private readonly ITracer _tracer;
        private readonly ICloudSdkConfiguration _cloudSdkConfiguration;

        public RebuildController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<RebuildController> logger, IFileUtility fileUtility, ITracer tracer,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration) : base(logger)
        {
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
            _zipUtility = zipUtility ?? throw new ArgumentNullException(nameof(zipUtility));
            _storeConfiguration = storeConfiguration ?? throw new ArgumentNullException(nameof(storeConfiguration));
            _processingConfiguration = processingConfiguration ?? throw new ArgumentNullException(nameof(processingConfiguration));
            _processingCancellationTokenSource = new CancellationTokenSource(_processingConfiguration.ProcessingTimeoutDuration);
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _cloudSdkConfiguration = cloudSdkConfiguration ?? throw new ArgumentNullException(nameof(cloudSdkConfiguration));
        }

        [HttpPost(Constants.Endpoints.FILE)]
        [HttpPost(Constants.Endpoints.ZIP_FILE)]
        public async Task<IActionResult> RebuildFromFormFile([FromForm][Required] IFormFile file)
        {
            bool zipRequest = Request.Path.ToString().ToLower().EndsWith(Constants.Endpoints.ZIP_FILE);
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(RebuildFromFormFile)} method invoked");

            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            string tempFolderPath = Path.Combine(Constants.VAR_PATH, $"{Guid.NewGuid()}");
            string extractedFolderPath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string extractedRebuildZipFilePath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string extractedRebuildFolderPath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Rebuild file");
            span.SetTag("Jaeger Testing Client", "POST api/Rebuild/file request");

            try
            {
                if (!_fileUtility.TryReadFormFile(file, out byte[] fileBytes))
                {
                    cloudProxyResponseModel.Errors.Add("Input file could not be read.");
                    return BadRequest(cloudProxyResponseModel);
                }

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(fileBytes, _cloudSdkConfiguration);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                fileId = descriptor.UUID.ToString();
                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.Outcome)
                {
                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Updating 'Original' store for {fileId}");
                    using (Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    (bool status, string error) = IsValidZip(originalStoreFilePath, null, extractedFolderPath);
                    if ((zipRequest && !status) || (!zipRequest && status))
                    {
                        cloudProxyResponseModel.Errors.Add(zipRequest ? "file should be a valid zip." : "file should not be a zip.");
                        return BadRequest(cloudProxyResponseModel);
                    }

                    _adaptationServiceClient.Connect();
                    ReturnOutcome outcome = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(outcome, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        if (zipRequest)
                        {
                            (bool status, string error) = IsValidZip(descriptor.RebuiltStoreFilePath, null, extractedRebuildFolderPath);
                            if (!status)
                            {
                                cloudProxyResponseModel.Errors.Add(error);
                                return BadRequest(cloudProxyResponseModel);
                            }

                            string[] files = Directory.GetFiles(extractedRebuildFolderPath, Constants.STAR, SearchOption.AllDirectories);

                            if (files.Any(x => Path.GetFileName(x).Equals(Constants.ERROR_REPORT_HTML_FILE_NAME)))
                            {
                                if (files.Length == 1)
                                {
                                    if (System.IO.File.Exists(files[0]))
                                    {
                                        cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(files[0]));
                                    }
                                    cloudProxyResponseModel.Status = ReturnOutcome.GW_FAILED;
                                    return BadRequest(cloudProxyResponseModel);
                                }
                                else
                                {
                                    Response.StatusCode = StatusCodes.Status207MultiStatus;
                                    return new FileContentResult(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                                }
                            }
                        }
                        return new FileContentResult(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing Timeout 'input' {fileId} exceeded {_processingConfiguration.ProcessingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileId} exceeded {_processingConfiguration.ProcessingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing 'input' {fileId} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileId} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                ClearStores(originalStoreFilePath, rebuiltStoreFilePath);
                AddHeaderToResponse(Constants.Header.FILE_ID, fileId);
                span.Finish();
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
        }

        [HttpPost(Constants.Endpoints.BASE64)]
        public async Task<IActionResult> RebuildFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(RebuildFromBase64)} method invoked");

            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Rebuild base64");
            span.SetTag("Jaeger Testing Client", "POST api/Rebuild/base64 request");

            try
            {
                if (!ModelState.IsValid)
                {
                    cloudProxyResponseModel.Errors.AddRange(ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage));
                    return BadRequest(cloudProxyResponseModel);
                }

                if (!_fileUtility.TryGetBase64File(request.Base64, out byte[] file))
                {
                    cloudProxyResponseModel.Errors.Add("Input file could not be decoded from base64.");
                    return BadRequest(cloudProxyResponseModel);
                }

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(file, _cloudSdkConfiguration);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                fileId = descriptor.UUID.ToString();
                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.Outcome)
                {
                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Updating 'Original' store for {fileId}");
                    using (Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create))
                    {
                        await fileStream.WriteAsync(file, 0, file.Length);
                    }

                    _adaptationServiceClient.Connect();
                    ReturnOutcome outcome = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(outcome, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        return Ok(Convert.ToBase64String(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath)));
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing Timeout 'input' {fileId} exceeded {_processingConfiguration.ProcessingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileId} exceeded {_processingConfiguration.ProcessingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing 'input' {fileId} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileId} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                ClearStores(originalStoreFilePath, rebuiltStoreFilePath);
                AddHeaderToResponse(Constants.Header.FILE_ID, fileId);
                span.Finish();
            }
        }

        [HttpPost(Constants.Endpoints.PROTECTED_ZIP_FILE)]
        public async Task<IActionResult> RebuildFromFormProtectedFile([FromForm][Required] IFormFile file, [FromForm][Required] string password)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(RebuildFromFormProtectedFile)} method invoked");

            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            string tempFolderPath = Path.Combine(Constants.VAR_PATH, $"{Guid.NewGuid()}");
            string protectedZipFilePath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string zipFilePath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string extractedFolderPath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string extractedRebuildZipFilePath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string extractedRebuildFolderPath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Rebuild protected file");
            span.SetTag("Jaeger Testing Client", "POST api/Rebuild/protectedzipfile request");

            try
            {
                if (!_fileUtility.TryReadFormFile(file, out byte[] fileBytes))
                {
                    cloudProxyResponseModel.Errors.Add("Input file could not be read.");
                    return BadRequest(cloudProxyResponseModel);
                }

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(fileBytes, _cloudSdkConfiguration);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                fileId = descriptor.UUID.ToString();
                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.Outcome)
                {
                    if (!Directory.Exists(tempFolderPath))
                    {
                        Directory.CreateDirectory(tempFolderPath);
                    }

                    using (Stream fileStream = new FileStream(protectedZipFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    (bool status, string error) = IsValidZip(protectedZipFilePath, password, extractedFolderPath);
                    if (!status)
                    {
                        cloudProxyResponseModel.Errors.Add(error);
                        return BadRequest(cloudProxyResponseModel);
                    }

                    _zipUtility.CreateZipFile(zipFilePath, null, extractedFolderPath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Updating 'Original' store for {fileId}");
                    using (Stream zipfileStream = new FileStream(zipFilePath, FileMode.Open))
                    {
                        using Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create);
                        await zipfileStream.CopyToAsync(fileStream);
                    }

                    _adaptationServiceClient.Connect();
                    ReturnOutcome outcome = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(outcome, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        (bool status, string error) = IsValidZip(descriptor.RebuiltStoreFilePath, null, extractedRebuildFolderPath);
                        if (!status)
                        {
                            cloudProxyResponseModel.Errors.Add(error);
                            return BadRequest(cloudProxyResponseModel);
                        }

                        _zipUtility.CreateZipFile(extractedRebuildZipFilePath, password, extractedRebuildFolderPath);
                        return new FileContentResult(System.IO.File.ReadAllBytes(extractedRebuildZipFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing Timeout 'input' {fileId} exceeded {_processingConfiguration.ProcessingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileId} exceeded {_processingConfiguration.ProcessingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing 'input' {fileId} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileId} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                ClearStores(originalStoreFilePath, rebuiltStoreFilePath);
                span.Finish();
                AddHeaderToResponse(Constants.Header.FILE_ID, fileId);
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
        }

        private (bool status, string error) IsValidZip(string zipFilePath, string password, string extractedFolderPath)
        {
            try
            {
                _zipUtility.ExtractZipFile(zipFilePath, password, extractedFolderPath);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
