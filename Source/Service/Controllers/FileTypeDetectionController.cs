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
    public class FileTypeDetectionController : CloudProxyController<FileTypeDetectionController>
    {
        public FileTypeDetectionController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<FileTypeDetectionController> logger, IFileUtility fileUtility,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration, IHttpService httpService) : base(logger, adaptationServiceClient, fileUtility, cloudSdkConfiguration,
                                                                                        processingConfiguration, storeConfiguration, zipUtility, httpService)
        {
        }

        [HttpPost(Constants.Endpoints.BASE64)]
        public async Task<IActionResult> DetermineFileTypeFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(DetermineFileTypeFromBase64)} method invoked");

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
                        (ReportInformation reportInformation, IActionResult Result) = await GetReportAndMetadataInformation(fileId, descriptor.AdaptationServiceResponse.ReportPresignedUrl, descriptor.AdaptationServiceResponse.MetaDataPresignedUrl);
                        if (reportInformation.ReportBytes == null)
                        {
                            return Result;
                        }

                        GWallInfo result = reportInformation.ReportXmlText.XmlStringToObject<GWallInfo>();
                        int.TryParse(result.DocumentStatistics.DocumentSummary.TotalSizeInBytes, out int fileSize);
                        return Ok(new
                        {
                            FileTypeName = result.DocumentStatistics.DocumentSummary.FileType,
                            FileSize = fileSize
                        });
                    case ReturnOutcome.GW_FAILED:
                        if (System.IO.File.Exists(rebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(await System.IO.File.ReadAllTextAsync(rebuiltStoreFilePath));
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
    }
}
