﻿using Amazon.S3.Util;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.HttpService;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.Web.Abstraction
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class CloudProxyController<TController> : ControllerBase, IDisposable
    {
        protected readonly ILogger<TController> _logger;
        protected readonly IFileUtility _fileUtility;
        protected readonly ICloudSdkConfiguration _cloudSdkConfiguration;
        protected readonly IProcessingConfiguration _processingConfiguration;
        protected readonly IStoreConfiguration _storeConfiguration;
        protected readonly IAdaptationServiceClient<AdaptationOutcomeProcessor> _adaptationServiceClient;
        protected readonly IZipUtility _zipUtility;
        protected readonly IHttpService _httpService;
        protected readonly List<FileType> _archiveTypes = new List<FileType> { FileType.Zip, FileType.Rar, FileType.Tar, FileType.SevenZip, FileType.Gzip };

        private UserAgentInfo _userAgentInfo;
        private bool _disposedValue;

        public CloudProxyController(ILogger<TController> logger, IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IFileUtility fileUtility,
            ICloudSdkConfiguration cloudSdkConfiguration, IProcessingConfiguration processingConfiguration, IStoreConfiguration storeConfiguration, IZipUtility zipUtility,
            IHttpService httpService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileUtility = fileUtility ?? throw new ArgumentNullException(nameof(fileUtility));
            _cloudSdkConfiguration = cloudSdkConfiguration ?? throw new ArgumentNullException(nameof(cloudSdkConfiguration));
            _processingConfiguration = processingConfiguration ?? throw new ArgumentNullException(nameof(processingConfiguration));
            _storeConfiguration = storeConfiguration ?? throw new ArgumentNullException(nameof(storeConfiguration));
            _adaptationServiceClient = adaptationServiceClient ?? throw new ArgumentNullException(nameof(adaptationServiceClient));
            _zipUtility = zipUtility ?? throw new ArgumentNullException(nameof(zipUtility));
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
        }

        protected void ClearStores(string originalStoreFilePath, string rebuiltStoreFilePath)
        {
            try
            {
                _logger.LogInformation($"Clearing stores '{originalStoreFilePath}' and {rebuiltStoreFilePath}");
                if (!string.IsNullOrEmpty(originalStoreFilePath))
                {
                    System.IO.File.Delete(originalStoreFilePath);
                }

                if (!string.IsNullOrEmpty(rebuiltStoreFilePath))
                {
                    System.IO.File.Delete(rebuiltStoreFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error whilst attempting to clear stores: {ex.Message}");
            }
        }

        protected (AmazonS3Uri amazonS3Uri, string error) AmazonS3Uri(string url)
        {
            try
            {
                return (new AmazonS3Uri(url), null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Invalid S3 URI {url}: {ex.Message}");
                return (null, ex.Message);
            }
        }

        protected void AddHeaderToResponse(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value) && !Response.Headers.ContainsKey(key))
            {
                Response.Headers.Add(key, value);
            }
        }

        protected UserAgentInfo UserAgentInfo
        {
            get
            {
                if (_userAgentInfo == null)
                {
                    _userAgentInfo = new UserAgentInfo(Request.Headers[Constants.UserAgent.USER_AGENT]);
                }
                return _userAgentInfo;
            }
        }

        protected async Task<(ReportInformation reportInformation, IActionResult Result)> GetReportAndMetadataInformation(string fileId, string cleanPresignedUrl, string metaDataPresignedUrl)
        {
            CloudProxyResponseModel cloudProxyResponseModel = new CloudProxyResponseModel();
            string reportFolderPath = Directory.GetDirectories(Constants.TRANSACTION_STORE_PATH, $"{ fileId}", SearchOption.AllDirectories).FirstOrDefault();
            ReportInformation reportInformation = new ReportInformation();

            // Running for minio version...
            if (!string.IsNullOrWhiteSpace(cleanPresignedUrl))
            {
                (byte[] Data, string Error) = await _httpService.GetFileBytes(cleanPresignedUrl);
                if (!string.IsNullOrEmpty(Error))
                {
                    cloudProxyResponseModel.Errors.Add($"Error while downloading report for {fileId} and errror detail is {Error}");
                    return (reportInformation, NotFound(cloudProxyResponseModel));
                }

                reportInformation.ReportBytes = Data;

                //TO DO: This check has to be removed because metadata presignedURL will always available for minio
                if (!string.IsNullOrWhiteSpace(metaDataPresignedUrl))
                {
                    (Data, Error) = await _httpService.GetFileBytes(metaDataPresignedUrl);
                    if (!string.IsNullOrEmpty(Error))
                    {
                        cloudProxyResponseModel.Errors.Add($"Error while downloading metadata for {fileId} and errror detail is {Error}");
                        return (reportInformation, NotFound(cloudProxyResponseModel));
                    }

                    reportInformation.MetadaBytes = Data;
                }
            }
            else  // Running for classic version...
            {
                if (string.IsNullOrEmpty(reportFolderPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report folder not exist for file {fileId}");
                    cloudProxyResponseModel.Errors.Add($"Report folder not exist for file {fileId}");
                    return (reportInformation, NotFound(cloudProxyResponseModel));
                }

                string reportPath = Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME);
                if (!System.IO.File.Exists(reportPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report xml not exist for file {fileId}");
                    cloudProxyResponseModel.Errors.Add($"Report xml not exist for file {fileId}");
                    return (reportInformation, NotFound(cloudProxyResponseModel));
                }

                reportInformation.ReportBytes = await System.IO.File.ReadAllBytesAsync(reportPath);
                string metaDataPath = Path.Combine(reportFolderPath, Constants.METADATA_JSON_FILE_NAME);

                if (!System.IO.File.Exists(metaDataPath))
                {
                    _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Metadata json not exist for file {fileId}");
                    cloudProxyResponseModel.Errors.Add($"Metadata json not exist for file {fileId}");
                    return (reportInformation, NotFound(cloudProxyResponseModel));
                }

                reportInformation.MetadaBytes = await System.IO.File.ReadAllBytesAsync(metaDataPath);
            }

            reportInformation.ReportXmlText = System.Text.Encoding.UTF8.GetString(reportInformation.ReportBytes);
            reportInformation.MetadaJsonText = reportInformation.MetadaBytes != null ? System.Text.Encoding.UTF8.GetString(reportInformation.MetadaBytes).FormattedJson() : null;
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Report json {reportInformation.ReportXmlText.XmlStringToJson()} for file {fileId}");
            return (reportInformation, null);
        }

        protected async Task<IActionResult> RebuildZipFile(Base64Request request = null, IFormFile formFile = null)
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
                        cloudProxyResponseModel.Errors.Add("Input file could not be parsed.");
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

                    _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Returning '{descriptor.AdaptationServiceResponse.FileOutcome}' Outcome for {fileId}");
                }

                switch (descriptor.AdaptationServiceResponse.FileOutcome)
                {
                    case ReturnOutcome.GW_REBUILT:
                        return await RebuildZipInfoByFileId(descriptor);
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

        protected async Task<IActionResult> RebuildZipInfoByFileId(AdaptionDescriptor descriptor)
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

                (ReportInformation reportInformation, IActionResult Result) = await GetReportAndMetadataInformation(fileIdString, descriptor.AdaptationServiceResponse.ReportPresignedUrl, descriptor.AdaptationServiceResponse.MetaDataPresignedUrl);
                if (reportInformation.ReportBytes == null)
                {
                    return Result;
                }

                GWallInfo gwInfo = reportInformation.ReportXmlText.XmlStringToObject<GWallInfo>();

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

                await System.IO.File.WriteAllTextAsync(Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME), reportInformation.ReportXmlText);
                await System.IO.File.WriteAllTextAsync(Path.Combine(tempFolderPath, Constants.METADATA_JSON_FILE_NAME), reportInformation.MetadaJsonText);
                await System.IO.File.WriteAllBytesAsync(Path.Combine(cleanFolderPath, $"{Path.GetFileName(rebuiltStoreFilePath)}.{gwInfo.DocumentStatistics.DocumentSummary.FileType}"), await System.IO.File.ReadAllBytesAsync(rebuiltStoreFilePath));

                _zipUtility.CreateZipFile(zipFileName, null, fileIdFolderPath);
                AddHeaderToResponse(Constants.Header.FILE_ID, fileIdString);
                return new FileContentResult(await System.IO.File.ReadAllBytesAsync(zipFileName), Constants.OCTET_STREAM_CONTENT_TYPE) { FileDownloadName = Path.GetFileName(zipFileName) };
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _fileUtility?.Dispose();
                    _cloudSdkConfiguration?.Dispose();
                    _processingConfiguration?.Dispose();
                    _storeConfiguration?.Dispose();
                    _adaptationServiceClient?.Dispose();
                    _zipUtility?.Dispose();
                    _httpService?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
