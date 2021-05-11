﻿using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.HttpService;
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
        private readonly IHttpService _httpService;
        private readonly IStoreConfiguration _storeConfiguration;
        private readonly IProcessingConfiguration _processingConfiguration;
        private readonly ITracer _tracer;
        private readonly ICloudSdkConfiguration _cloudSdkConfiguration;

        public RebuildController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<RebuildController> logger, IFileUtility fileUtility, ITracer tracer,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration, IHttpService httpService) : base(logger)
        {
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
            _zipUtility = zipUtility ?? throw new ArgumentNullException(nameof(zipUtility));
            _storeConfiguration = storeConfiguration ?? throw new ArgumentNullException(nameof(storeConfiguration));
            _processingConfiguration = processingConfiguration ?? throw new ArgumentNullException(nameof(processingConfiguration));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _cloudSdkConfiguration = cloudSdkConfiguration ?? throw new ArgumentNullException(nameof(cloudSdkConfiguration));
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
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
                CancellationToken processingCancellationToken = new CancellationTokenSource(_processingConfiguration.ProcessingTimeoutDuration).Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.AdaptationServiceResponse.FileOutcome)
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
                    IAdaptationServiceResponse adaptationServiceResponse = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(adaptationServiceResponse, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.AdaptationServiceResponse.FileOutcome}' Outcome for {fileId}");
                }

                switch (descriptor.AdaptationServiceResponse.FileOutcome)
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
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
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
                CancellationToken processingCancellationToken = new CancellationTokenSource(_processingConfiguration.ProcessingTimeoutDuration).Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.AdaptationServiceResponse.FileOutcome)
                {
                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Updating 'Original' store for {fileId}");
                    using (Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create))
                    {
                        await fileStream.WriteAsync(file, 0, file.Length);
                    }

                    _adaptationServiceClient.Connect();
                    IAdaptationServiceResponse adaptationServiceResponse = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(adaptationServiceResponse, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{adaptationServiceResponse.FileOutcome}' Outcome for {fileId}");
                }

                switch (descriptor.AdaptationServiceResponse.FileOutcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        return Ok(Convert.ToBase64String(System.IO.File.ReadAllBytes(descriptor.RebuiltStoreFilePath)));
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
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
                CancellationToken processingCancellationToken = new CancellationTokenSource(_processingConfiguration.ProcessingTimeoutDuration).Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.AdaptationServiceResponse.FileOutcome)
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
                    IAdaptationServiceResponse adaptationServiceResponse = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(adaptationServiceResponse, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.AdaptationServiceResponse.FileOutcome}' Outcome for {fileId}");
                }

                switch (descriptor.AdaptationServiceResponse.FileOutcome)
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
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
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


        [HttpPost("minio")]
        public async Task<IActionResult> RebuildFromFormFileMinio([FromForm][Required] IFormFile file)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(RebuildFromFormFileMinio)} method invoked");

            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Rebuild file");
            span.SetTag("Jaeger Testing Client", "POST api/Rebuild/minio request");

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
                CancellationToken processingCancellationToken = new CancellationTokenSource(_processingConfiguration.ProcessingTimeoutDuration).Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_storeConfiguration.OriginalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileId);

                if (ReturnOutcome.GW_REBUILT != descriptor.AdaptationServiceResponse.FileOutcome)
                {
                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Updating 'Original' store for {fileId}");
                    using (Stream fileStream = new FileStream(originalStoreFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    _adaptationServiceClient.Connect();
                    IAdaptationServiceResponse adaptationServiceResponse = _adaptationServiceClient.AdaptationRequest(descriptor.UUID, originalStoreFilePath, rebuiltStoreFilePath, processingCancellationToken);
                    descriptor.Update(adaptationServiceResponse, originalStoreFilePath, rebuiltStoreFilePath);

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.AdaptationServiceResponse.FileOutcome}' Outcome for {fileId}");
                }

                switch (descriptor.AdaptationServiceResponse.FileOutcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        return await RebuildZipInfoByFileId(descriptor, span);
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
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

        private async Task<IActionResult> RebuildZipInfoByFileId(AdaptionDescriptor descriptor, ISpan span)
        {
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            if (descriptor.UUID == Guid.Empty)
            {
                cloudProxyResponseModel.Errors.Add($"The value {descriptor.UUID} should not be empty.");
                return BadRequest(cloudProxyResponseModel);
            }

            string fileIdString = descriptor.UUID.ToString();
            string zipFileName = Path.Combine(Constants.VAR_PATH, $"{fileIdString}{Constants.ZIP_EXTENSION}");
            string fileIdFolderPath = Path.Combine(Constants.VAR_PATH, fileIdString);
            string tempFolderPath = Path.Combine(fileIdFolderPath, fileIdString);

            try
            {
                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileIdString}");

                string transactionFolderPath = Directory.GetDirectories(Constants.TRANSACTION_STORE_PATH, $"{ fileIdString}", SearchOption.AllDirectories).FirstOrDefault();

                if (string.IsNullOrEmpty(transactionFolderPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report folder not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Report folder not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

                string reportPath = Path.Combine(transactionFolderPath, Constants.REPORT_XML_FILE_NAME);
                if (!System.IO.File.Exists(reportPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report xml not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Report xml not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

                //XmlSerializer serializer = new XmlSerializer(typeof(GWallInfo));
                //GWallInfo gwInfo = null;
                //using (FileStream fileStream = new FileStream(reportPath, FileMode.Open))
                //{
                //    gwInfo = (GWallInfo)serializer.Deserialize(fileStream);
                //}

                //string metaDataPath = Path.Combine(transactionFolderPath, Constants.METADATA_JSON_FILE_NAME);
                //if (!System.IO.File.Exists(metaDataPath))
                //{
                //    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Metadata json not exist for file {fileIdString}");
                //    cloudProxyResponseModel.Errors.Add($"Metadata json not exist for file {fileIdString}");
                //    return NotFound(cloudProxyResponseModel);
                //}

                if (!Directory.Exists(tempFolderPath))
                {
                    Directory.CreateDirectory(tempFolderPath);
                }

                string reportFolderPath = Path.Combine(tempFolderPath, Constants.REPORT_FOLDER_NAME);
                if (!Directory.Exists(reportFolderPath))
                {
                    Directory.CreateDirectory(reportFolderPath);
                }

                string cleanFolderPath = Path.Combine(tempFolderPath, Constants.CLEAN_FOLDER_NAME);
                if (!Directory.Exists(cleanFolderPath))
                {
                    Directory.CreateDirectory(cleanFolderPath);
                }

                string minIoFolderPath = Path.Combine(tempFolderPath, "MinIo");
                if (!Directory.Exists(minIoFolderPath))
                {
                    Directory.CreateDirectory(minIoFolderPath);
                }

                string rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileIdString);
                if (!System.IO.File.Exists(rebuiltStoreFilePath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Rebuild file not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Rebuild file not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

                //await System.IO.File.WriteAllTextAsync(Path.Combine(tempFolderPath, Constants.METADATA_JSON_FILE_NAME), (await System.IO.File.ReadAllTextAsync(metaDataPath)).FormattedJson());
                await System.IO.File.WriteAllBytesAsync(Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME), await System.IO.File.ReadAllBytesAsync(reportPath));
                await System.IO.File.WriteAllBytesAsync(Path.Combine(cleanFolderPath, $"{Path.GetFileName(rebuiltStoreFilePath)}.pdf"), await System.IO.File.ReadAllBytesAsync(rebuiltStoreFilePath));

                (byte[] Data, string Error) resp = await _httpService.GetFileBytes(descriptor.AdaptationServiceResponse.CleanPresignedUrl);
                if (resp.Data != null)
                {
                    _logger.LogInformation($"CleanFileByte::{resp.Data.Length}");
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(minIoFolderPath, "cleanpdf.pdf"), resp.Data);
                }
                else
                {
                    await System.IO.File.WriteAllTextAsync(Path.Combine(minIoFolderPath, "cleanpdf.txt"), resp.Error);
                }

                resp = await _httpService.GetFileBytes(descriptor.AdaptationServiceResponse.ReportPresignedUrl);
                if (resp.Data != null)
                {
                    _logger.LogInformation($"ReportFileByte::{resp.Data.Length}");
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(minIoFolderPath, Constants.REPORT_XML_FILE_NAME), resp.Data);
                }
                else
                {
                    await System.IO.File.WriteAllTextAsync(Path.Combine(minIoFolderPath, "report.txt"), resp.Error);
                }

                resp = await _httpService.GetFileBytes(descriptor.AdaptationServiceResponse.SourcePresignedUrl);
                if (resp.Data != null)
                {
                    _logger.LogInformation($"SourceFileByte::{resp.Data.Length}");
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(minIoFolderPath, "sourcepdf.pdf"), resp.Data);
                }
                else
                {
                    await System.IO.File.WriteAllTextAsync(Path.Combine(minIoFolderPath, "sourcepdf.txt"), resp.Error);
                }

                if (System.IO.File.Exists(descriptor.AdaptationServiceResponse.RebuiltFileLocation))
                {
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(minIoFolderPath, "RebuiltFileLocation.pdf"), await System.IO.File.ReadAllBytesAsync(descriptor.AdaptationServiceResponse.RebuiltFileLocation));
                }

                if (System.IO.File.Exists(descriptor.AdaptationServiceResponse.SourceFileLocation))
                {
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(minIoFolderPath, "SourceFileLocation.pdf"), await System.IO.File.ReadAllBytesAsync(descriptor.AdaptationServiceResponse.SourceFileLocation));
                }

                await System.IO.File.WriteAllTextAsync(Path.Combine(minIoFolderPath, "response.json"), Newtonsoft.Json.JsonConvert.SerializeObject(descriptor.AdaptationServiceResponse).FormattedJson());
                _zipUtility.CreateZipFile(zipFileName, null, fileIdFolderPath);
                AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                return new FileContentResult(System.IO.File.ReadAllBytes(zipFileName), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = Path.GetFileName(zipFileName) };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Errors.Add($"Error Processing 'input' {fileIdString} and error detail is {ex.Message}");
                cloudProxyResponseModel.Status = ReturnOutcome.GW_ERROR;
                return StatusCode(StatusCodes.Status500InternalServerError, cloudProxyResponseModel);
            }
            finally
            {
                span.Finish();

                if (Directory.Exists(fileIdFolderPath))
                {
                    Directory.Delete(fileIdFolderPath, true);
                }

                if (System.IO.File.Exists(zipFileName))
                {
                    System.IO.File.Delete(zipFileName);
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
