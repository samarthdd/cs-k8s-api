using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Glasswall.CloudProxy.Api.Models;
using Glasswall.CloudProxy.Api.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Glasswall.CloudProxy.Common.Web.Models;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class AnalyseController : CloudProxyController<AnalyseController>
    {
        private readonly IAdaptationServiceClient<AdaptationOutcomeProcessor> _adaptationServiceClient;
        private readonly IFileUtility _fileUtility;
        private readonly CancellationTokenSource _processingCancellationTokenSource;
        private readonly TimeSpan _processingTimeoutDuration;
        private readonly string OriginalStorePath;
        private readonly string RebuiltStorePath;

        public AnalyseController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<AnalyseController> logger, IFileUtility fileUtility) : base(logger)
        {
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
            if (storeConfiguration == null) throw new ArgumentNullException(nameof(storeConfiguration));
            if (processingConfiguration == null) throw new ArgumentNullException(nameof(processingConfiguration));
            _processingTimeoutDuration = processingConfiguration.ProcessingTimeoutDuration;
            _processingCancellationTokenSource = new CancellationTokenSource(_processingTimeoutDuration);

            OriginalStorePath = storeConfiguration.OriginalStorePath;
            RebuiltStorePath = storeConfiguration.RebuiltStorePath;
        }

        [HttpPost("base64")]
        public async Task<IActionResult> AnalyseFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation("'{0}' method invoked", nameof(AnalyseFromBase64));
            string originalStoreFilePath = string.Empty;
            string rebuiltStoreFilePath = string.Empty;
            String fileIdString = "";
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

                AdaptionDescriptor descriptor = AdaptionCache.Instance.GetDescriptor(file);
                if (null == descriptor)
                {
                    cloudProxyResponseModel.Errors.Add("Cannot create a cache entry for the file.");
                    return BadRequest(cloudProxyResponseModel);
                }

                Guid fileId = descriptor.UUID;
                fileIdString = fileId.ToString();

                CancellationToken processingCancellationToken = _processingCancellationTokenSource.Token;

                _logger.LogInformation($"Using store locations '{OriginalStorePath}' and '{RebuiltStorePath}' for {fileId}");

                originalStoreFilePath = Path.Combine(OriginalStorePath, fileId.ToString());
                rebuiltStoreFilePath = Path.Combine(RebuiltStorePath, fileId.ToString());


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

                    _logger.LogInformation($"Returning '{descriptor.Outcome}' Outcome for {fileId}");
                }

                switch (descriptor.Outcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        string reportFolderPath = Directory.GetDirectories(Constants.TRANSACTION_STORE_PATH, $"{ fileId}", SearchOption.AllDirectories).FirstOrDefault();
                        if (string.IsNullOrEmpty(descriptor.RebuiltStoreFilePath))
                        {
                            _logger.LogWarning($"Report folder not exist for file {fileId}");
                            cloudProxyResponseModel.Errors.Add($"Report folder not exist for file {fileId}");
                            return NotFound(cloudProxyResponseModel);
                        }

                        string reportPath = Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME);
                        if (!System.IO.File.Exists(reportPath))
                        {
                            _logger.LogWarning($"Report xml not exist for file {fileId}");
                            cloudProxyResponseModel.Errors.Add($"Report xml not exist for file {fileId}");
                            return NotFound(cloudProxyResponseModel);
                        }

                        AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                        return new FileContentResult(System.IO.File.ReadAllBytes(reportPath), "application/octet-stream") { FileDownloadName = Constants.REPORT_XML_FILE_NAME };
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
            }
        }
    }
}
