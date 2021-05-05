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
using System.Xml.Serialization;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class AnalyseController : CloudProxyController<AnalyseController>
    {
        private readonly IAdaptationServiceClient<AdaptationOutcomeProcessor> _adaptationServiceClient;
        private readonly IFileUtility _fileUtility;
        private readonly IStoreConfiguration _storeConfiguration;
        private readonly IProcessingConfiguration _processingConfiguration;
        private readonly ITracer _tracer;
        private readonly IZipUtility _zipUtility;
        private readonly ICloudSdkConfiguration _cloudSdkConfiguration;

        public AnalyseController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<AnalyseController> logger, IFileUtility fileUtility, ITracer tracer,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration) : base(logger)
        {
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
            _storeConfiguration = storeConfiguration ?? throw new ArgumentNullException(nameof(storeConfiguration));
            _processingConfiguration = processingConfiguration ?? throw new ArgumentNullException(nameof(processingConfiguration));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _zipUtility = zipUtility ?? throw new ArgumentNullException(nameof(zipUtility));
            _cloudSdkConfiguration = cloudSdkConfiguration ?? throw new ArgumentNullException(nameof(cloudSdkConfiguration));
        }

        [HttpPost(Constants.Endpoints.BASE64)]
        public async Task<IActionResult> AnalyseFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(AnalyseFromBase64)} method invoked");
            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Analyse base64");
            span.SetTag("Jaeger Testing Client", "POST api/Analyse/base64 request");

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

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        string reportFolderPath = Directory.GetDirectories(Constants.TRANSACTION_STORE_PATH, $"{ fileId}", SearchOption.AllDirectories).FirstOrDefault();
                        if (string.IsNullOrEmpty(descriptor.RebuiltStoreFilePath))
                        {
                            _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report folder not exist for file {fileId}");
                            cloudProxyResponseModel.Errors.Add($"Report folder not exist for file {fileId}");
                            return NotFound(cloudProxyResponseModel);
                        }

                        string reportPath = Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME);
                        if (!System.IO.File.Exists(reportPath))
                        {
                            _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report xml not exist for file {fileId}");
                            cloudProxyResponseModel.Errors.Add($"Report xml not exist for file {fileId}");
                            return NotFound(cloudProxyResponseModel);
                        }
                        return new FileContentResult(System.IO.File.ReadAllBytes(reportPath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = Constants.REPORT_XML_FILE_NAME };
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

        [HttpGet(Constants.Endpoints.XML_REPORT)]
        public IActionResult GetXMLReportByFileId([Required] Guid fileId)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(GetXMLReportByFileId)} method invoked");

            string fileIdString = string.Empty;
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Get::api/Analyse/xmlreport");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Analyse xmlreport");
            span.SetTag("Jaeger Testing Client", "GET api/Analyse/xmlreport request");

            try
            {
                if (fileId == Guid.Empty)
                {
                    cloudProxyResponseModel.Errors.Add($"The value {fileId} should not be empty.");
                    return BadRequest(cloudProxyResponseModel);
                }

                fileIdString = fileId.ToString();

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_storeConfiguration.OriginalStorePath}' and '{_storeConfiguration.RebuiltStorePath}' for {fileIdString}");

                string reportFolderPath = Directory.GetDirectories(Constants.TRANSACTION_STORE_PATH, $"{ fileIdString}", SearchOption.AllDirectories).FirstOrDefault();

                if (string.IsNullOrEmpty(reportFolderPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report folder not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Report folder not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

                string reportPath = Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME);
                if (!System.IO.File.Exists(reportPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report xml not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Report xml not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

                AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                return new FileContentResult(System.IO.File.ReadAllBytes(reportPath), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = Constants.REPORT_XML_FILE_NAME };
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
            }
        }

        [HttpGet(Constants.Endpoints.REBUILD_ZIP)]
        public async Task<IActionResult> GetRebuildZipFromFileId([Required] Guid fileId)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(GetRebuildZipFromFileId)} method invoked");
            ISpanBuilder builder = _tracer.BuildSpan("Get::api/Analyse/rebuildzip");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Analyse GetRebuildZipFromFileId");
            span.SetTag("Jaeger Testing Client", "GET api/Analyse/rebuildzip request");
            return await RebuildZipInfoByFileId(fileId, span);
        }

        [HttpPost(Constants.Endpoints.REBUILD_ZIP_FROM_BASE64)]
        public async Task<IActionResult> GetRebuildZipFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(GetRebuildZipFromBase64)} method invoked");
            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Analyse GetRebuildZipFromBase64");
            span.SetTag("Jaeger Testing Client", "POST api/Analyse/rebuild-zip-from-base64 request");
            return await RebuildZipFile(span, request);
        }

        [HttpPost(Constants.Endpoints.REBUILD_ZIP_FROM_FILE)]
        public async Task<IActionResult> GetRebuildZipFromFormFile([FromForm][Required] IFormFile file)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(GetRebuildZipFromFormFile)} method invoked");
            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("Analyse GetRebuildZipFromFormFile");
            span.SetTag("Jaeger Testing Client", "POST api/Analyse/rebuild-zip-from-file request");
            return await RebuildZipFile(span, formFile: file);
        }

        private async Task<IActionResult> RebuildZipFile(ISpan span, Base64Request request = null, IFormFile formFile = null)
        {
            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            try
            {
                byte[] file = null;
                if (!ModelState.IsValid)
                {
                    cloudProxyResponseModel.Errors.AddRange(ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage));
                    return BadRequest(cloudProxyResponseModel);
                }

                if (request != null)
                {
                    if (!_fileUtility.TryGetBase64File(request.Base64, out file))
                    {
                        cloudProxyResponseModel.Errors.Add("Input file could not be decoded from base64.");
                        return BadRequest(cloudProxyResponseModel);
                    }
                }

                if (formFile != null)
                {
                    if (!_fileUtility.TryReadFormFile(formFile, out file))
                    {
                        cloudProxyResponseModel.Errors.Add("Input file could not be decoded from base64.");
                        return BadRequest(cloudProxyResponseModel);
                    }
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

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        return await RebuildZipInfoByFileId(descriptor.UUID, span);
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

        private async Task<IActionResult> RebuildZipInfoByFileId(Guid fileId, ISpan span)
        {
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            if (fileId == Guid.Empty)
            {
                cloudProxyResponseModel.Errors.Add($"The value {fileId} should not be empty.");
                return BadRequest(cloudProxyResponseModel);
            }

            string fileIdString = fileId.ToString();
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

                XmlSerializer serializer = new XmlSerializer(typeof(GWallInfo));
                GWallInfo gwInfo = null;
                using (FileStream fileStream = new FileStream(reportPath, FileMode.Open))
                {
                    gwInfo = (GWallInfo)serializer.Deserialize(fileStream);
                }

                string metaDataPath = Path.Combine(transactionFolderPath, Constants.METADATA_JSON_FILE_NAME);
                if (!System.IO.File.Exists(metaDataPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Metadata json not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Metadata json not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

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

                string rebuiltStoreFilePath = Path.Combine(_storeConfiguration.RebuiltStorePath, fileIdString);
                if (!System.IO.File.Exists(rebuiltStoreFilePath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Rebuild file not exist for file {fileIdString}");
                    cloudProxyResponseModel.Errors.Add($"Rebuild file not exist for file {fileIdString}");
                    return NotFound(cloudProxyResponseModel);
                }

                await System.IO.File.WriteAllTextAsync(Path.Combine(tempFolderPath, Constants.METADATA_JSON_FILE_NAME), (await System.IO.File.ReadAllTextAsync(metaDataPath)).FormattedJson());
                await System.IO.File.WriteAllBytesAsync(Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME), await System.IO.File.ReadAllBytesAsync(reportPath));
                await System.IO.File.WriteAllBytesAsync(Path.Combine(cleanFolderPath, $"{Path.GetFileName(rebuiltStoreFilePath)}.{gwInfo.DocumentStatistics.DocumentSummary.FileType}"), await System.IO.File.ReadAllBytesAsync(rebuiltStoreFilePath));

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
    }
}
