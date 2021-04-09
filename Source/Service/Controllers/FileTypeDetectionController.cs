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
using System.Xml.Serialization;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class FileTypeDetectionController : CloudProxyController<FileTypeDetectionController>
    {
        private readonly IAdaptationServiceClient<AdaptationOutcomeProcessor> _adaptationServiceClient;
        private readonly IFileUtility _fileUtility;
        private readonly CancellationTokenSource _processingCancellationTokenSource;
        private readonly TimeSpan _processingTimeoutDuration;
        private readonly string _originalStorePath;
        private readonly string _rebuiltStorePath;
        private readonly ITracer _tracer;

        public FileTypeDetectionController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<FileTypeDetectionController> logger, IFileUtility fileUtility, ITracer tracer) : base(logger)
        {
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
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

        [HttpPost("base64")]
        public async Task<IActionResult> DetermineFileTypeFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(DetermineFileTypeFromBase64)} method invoked");

            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            string fileId = string.Empty;
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();

            ISpanBuilder builder = _tracer.BuildSpan("Post::Data");
            ISpan span = builder.Start();

            // Set some context data
            span.Log("File Type Detection base64");
            span.SetTag("Jaeger Testing Client", "POST api/FileTypeDetection/base64 request");

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

                fileId = descriptor.UUID.ToString();
                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Using store locations '{_originalStorePath}' and '{_rebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(_originalStorePath, fileId);
                rebuiltStoreFilePath = Path.Combine(_rebuiltStorePath, fileId);

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

                        XmlSerializer serializer = new XmlSerializer(typeof(GWallInfo));
                        GWallInfo result = null;
                        using (FileStream fileStream = new FileStream(reportPath, FileMode.Open))
                        {
                            result = (GWallInfo)serializer.Deserialize(fileStream);
                        }

                        int.TryParse(result.DocumentStatistics.DocumentSummary.TotalSizeInBytes, out int fileSize);
                        AddHeaderToResponse(Constants.Header.FILE_ID, fileId);
                        return Ok(new
                        {
                            FileTypeName = result.DocumentStatistics.DocumentSummary.FileType,
                            FileSize = fileSize
                        });
                    case ReturnOutcome.GW_FAILED:
                    default:
                        if (System.IO.File.Exists(rebuiltStoreFilePath))
                        {
                            cloudProxyResponseModel.Errors.Add(System.IO.File.ReadAllText(rebuiltStoreFilePath));
                        }
                        cloudProxyResponseModel.Status = descriptor.Outcome;
                        return BadRequest(cloudProxyResponseModel);
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, $"[{UserAgentInfo.ClientTypeString}]:: Error Processing Timeout 'input' {fileId} exceeded {_processingTimeoutDuration.TotalSeconds}s");
                cloudProxyResponseModel.Errors.Add($"Error Processing Timeout 'input' {fileId} exceeded {_processingTimeoutDuration.TotalSeconds}s");
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
            }
        }
    }
}
