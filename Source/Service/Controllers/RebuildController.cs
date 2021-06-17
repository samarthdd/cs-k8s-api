using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.HttpService;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        public RebuildController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<RebuildController> logger, IFileUtility fileUtility,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration, IHttpService httpService) : base(logger, adaptationServiceClient, fileUtility, cloudSdkConfiguration,
                                                                                                                processingConfiguration, storeConfiguration, zipUtility, httpService)
        {
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

                    FileTypeDetectionResponse fileType = _fileUtility.DetermineFileType(fileBytes);
                    if ((zipRequest && !_archiveTypes.Contains(fileType.FileType)) || (!zipRequest && _archiveTypes.Contains(fileType.FileType)))
                    {
                        cloudProxyResponseModel.Errors.Add(zipRequest ? "This endpoint accepts only the zip file." : "This endpoint doesn't accept the zip file.");
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
                            (bool status, string error) = ExtractZipFile(descriptor.RebuiltStoreFilePath, null, extractedRebuildFolderPath);
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
                                        cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(files[0]));
                                    }
                                    cloudProxyResponseModel.Status = ReturnOutcome.GW_FAILED;
                                    return BadRequest(cloudProxyResponseModel);
                                }
                                else
                                {
                                    Response.StatusCode = StatusCodes.Status207MultiStatus;
                                    return new FileContentResult(await System.IO.File.ReadAllBytesAsync(descriptor.RebuiltStoreFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                                }
                            }
                        }

                        await GetReportAndMetadataInformation(fileId, descriptor.AdaptationServiceResponse.ReportPresignedUrl, descriptor.AdaptationServiceResponse.MetaDataPresignedUrl);
                        return new FileContentResult(await System.IO.File.ReadAllBytesAsync(descriptor.RebuiltStoreFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
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
                        await GetReportAndMetadataInformation(fileId, descriptor.AdaptationServiceResponse.ReportPresignedUrl, descriptor.AdaptationServiceResponse.MetaDataPresignedUrl);
                        return Ok(Convert.ToBase64String(await System.IO.File.ReadAllBytesAsync(descriptor.RebuiltStoreFilePath)));
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
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

                    FileTypeDetectionResponse fileType = _fileUtility.DetermineFileType(fileBytes);
                    if (!_archiveTypes.Contains(fileType.FileType))
                    {
                        cloudProxyResponseModel.Errors.Add("This endpoint accepts only zip file.");
                        return BadRequest(cloudProxyResponseModel);
                    }

                    (bool status, string error) = ExtractZipFile(protectedZipFilePath, password, extractedFolderPath);
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
                        (bool status, string error) = ExtractZipFile(descriptor.RebuiltStoreFilePath, null, extractedRebuildFolderPath);
                        if (!status)
                        {
                            cloudProxyResponseModel.Errors.Add(error);
                            return BadRequest(cloudProxyResponseModel);
                        }

                        _zipUtility.CreateZipFile(extractedRebuildZipFilePath, password, extractedRebuildFolderPath);
                        await GetReportAndMetadataInformation(fileId, descriptor.AdaptationServiceResponse.ReportPresignedUrl, descriptor.AdaptationServiceResponse.MetaDataPresignedUrl);
                        return new FileContentResult(await System.IO.File.ReadAllBytesAsync(extractedRebuildZipFilePath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = file.FileName ?? "Unknown" };
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_UNPROCESSED:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
                        return BadRequest(cloudProxyResponseModel);
                    case ReturnOutcome.GW_ERROR:
                    default:
                        if (System.IO.File.Exists(descriptor.RebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(descriptor.RebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.AdaptationServiceResponse.FileOutcome;
                        cloudProxyResponseModel.RebuildProcessingStatus = descriptor.AdaptationServiceResponse.RebuildProcessingStatus;
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
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
        }

        private (bool status, string error) ExtractZipFile(string zipFilePath, string password, string extractedFolderPath)
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
