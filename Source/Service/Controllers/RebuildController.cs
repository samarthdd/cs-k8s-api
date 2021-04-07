using Glasswall.CloudProxy.Api.Models;
using Glasswall.CloudProxy.Api.Utilities;
using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
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
        private readonly CancellationTokenSource _processingCancellationTokenSource;
        private readonly TimeSpan _processingTimeoutDuration;
        private readonly string _originalStorePath;
        private readonly string _rebuiltStorePath;
        private readonly ITracer _tracer;

        public RebuildController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<RebuildController> logger, IFileUtility fileUtility, ITracer tracer,
            IZipUtility zipUtility) : base(logger)
        {
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
            _zipUtility = zipUtility ?? throw new ArgumentNullException(nameof(zipUtility));
            if (storeConfiguration == null)
            {
                throw new ArgumentNullException(nameof(storeConfiguration));
            }

            if (processingConfiguration == null)
            {
                throw new ArgumentNullException(nameof(processingConfiguration));
            }

            _processingTimeoutDuration = processingConfiguration.ProcessingTimeoutDuration;
            _processingCancellationTokenSource = new CancellationTokenSource(_processingTimeoutDuration);

            _originalStorePath = storeConfiguration.OriginalStorePath;
            _rebuiltStorePath = storeConfiguration.RebuiltStorePath;

            _tracer = tracer;
        }

        [HttpPost("file")]
        [HttpPost("zipfile")]
        public async Task<IActionResult> RebuildFromFormFile([FromForm][Required] IFormFile file)
        {
            _logger.LogInformation("'{0}' method invoked", nameof(RebuildFromFormFile));
            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileIdString = "";
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

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(fileBytes);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                Guid fileId = descriptor.UUID;
                fileIdString = fileId.ToString();

                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"Using store locations '{_originalStorePath}' and '{_rebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_originalStorePath, fileId.ToString());
                rebuiltStoreFilePath = Path.Combine(_rebuiltStorePath, fileId.ToString());

                if (ReturnOutcome.GW_REBUILT != descriptor.Outcome)
                {
                    _logger.LogInformation($"Updating 'Original' store for {fileId}");
                    using (Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    _adaptationServiceClient.Connect();
                    ReturnOutcome outcome = _adaptationServiceClient.AdaptationRequest(fileId, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(outcome, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                        return new FileContentResult(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                    case ReturnOutcome.GW_FAILED:
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
                _logger.LogError(oce, $"Error Processing Timeout 'input' {fileIdString} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileIdString} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                ClearStores(originalStoreFilePath, rebuiltStoreFilePath);
                span.Finish();
            }
        }

        [HttpPost("base64")]
        public async Task<IActionResult> RebuildFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation("'{0}' method invoked", nameof(RebuildFromBase64));
            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileIdString = "";
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

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(file);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                Guid fileId = descriptor.UUID;
                fileIdString = fileId.ToString();

                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"Using store locations '{_originalStorePath}' and '{_rebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_originalStorePath, fileId.ToString());
                rebuiltStoreFilePath = Path.Combine(_rebuiltStorePath, fileId.ToString());

                if (ReturnOutcome.GW_REBUILT != descriptor.Outcome)
                {
                    _logger.LogInformation($"Updating 'Original' store for {fileId}");
                    using (Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create))
                    {
                        await fileStream.WriteAsync(file, 0, file.Length);
                    }

                    _adaptationServiceClient.Connect();
                    ReturnOutcome outcome = _adaptationServiceClient.AdaptationRequest(fileId, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(outcome, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"Returning '{outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                        return Ok(Convert.ToBase64String(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath)));
                    case ReturnOutcome.GW_FAILED:
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
                _logger.LogError(oce, $"Error Processing Timeout 'input' {fileIdString} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileIdString} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                ClearStores(originalStoreFilePath, rebuiltStoreFilePath);
                span.Finish();
            }
        }

        [HttpPost("protectedzipfile")]
        public async Task<IActionResult> RebuildFromFormProtectedFile([FromForm][Required] IFormFile file, [FromForm][Required] string password)
        {
            _logger.LogInformation("'{0}' method invoked", nameof(RebuildFromFormProtectedFile));

            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileIdString = string.Empty;
            string tempFolderPath = Path.Combine(Constants.VAR_PATH, $"{Guid.NewGuid()}");
            string protectedZipFilePath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string zipFilePath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
            string extractedFolderPath = Path.Combine(tempFolderPath, $"{Guid.NewGuid()}");
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

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(fileBytes);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                Guid fileId = descriptor.UUID;
                fileIdString = fileId.ToString();

                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"Using store locations '{_originalStorePath}' and '{_rebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_originalStorePath, fileId.ToString());
                rebuiltStoreFilePath = Path.Combine(_rebuiltStorePath, fileId.ToString());

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

                    try
                    {
                        _zipUtility.ExtractZipFile(protectedZipFilePath, password, extractedFolderPath);
                    }
                    catch (Exception ex)
                    {
                        cloudProxyResponseModel.Errors.Add(ex.Message);
                        return BadRequest(cloudProxyResponseModel);
                    }

                    _zipUtility.CreateZipFile(zipFilePath, null, extractedFolderPath);

                    _logger.LogInformation($"Updating 'Original' store for {fileId}");
                    using (Stream zipfileStream = new FileStream(zipFilePath, FileMode.Open))
                    {
                        using Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create);
                        await zipfileStream.CopyToAsync(fileStream);
                    }

                    _adaptationServiceClient.Connect();
                    ReturnOutcome outcome = _adaptationServiceClient.AdaptationRequest(fileId, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(outcome, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                        return new FileContentResult(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                    case ReturnOutcome.GW_FAILED:
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
                _logger.LogError(oce, $"Error Processing Timeout 'input' {fileIdString} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileIdString} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                ClearStores(originalStoreFilePath, rebuiltStoreFilePath);
                span.Finish();
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
        }
    }
}
