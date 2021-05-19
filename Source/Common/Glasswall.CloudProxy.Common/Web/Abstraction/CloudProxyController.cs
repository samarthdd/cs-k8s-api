using Amazon.S3.Util;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Common.Web.Abstraction
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class CloudProxyController<TController> : ControllerBase
    {
        protected readonly ILogger<TController> _logger;
        private UserAgentInfo _userAgentInfo;

        public CloudProxyController(ILogger<TController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected void ClearStores(string originalStoreFilePath, string rebuiltStoreFilePath)
        {
            try
            {
                _logger.LogInformation($"Clearing stores '{originalStoreFilePath}' and {rebuiltStoreFilePath}");
                //if (!string.IsNullOrEmpty(originalStoreFilePath))
                //{
                //    System.IO.File.Delete(originalStoreFilePath);
                //}

                //if (!string.IsNullOrEmpty(rebuiltStoreFilePath))
                //{
                //    System.IO.File.Delete(rebuiltStoreFilePath);
                //}
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

        protected async Task<(byte[] ReportBytes, string ReportText, IActionResult Result)> GetReportXmlData(string fileId, CloudProxyResponseModel cloudProxyResponseModel)
        {
            string reportFolderPath = Directory.GetDirectories(Constants.TRANSACTION_STORE_PATH, $"{ fileId}", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(reportFolderPath))
            {
                _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report folder not exist for file {fileId}");
                cloudProxyResponseModel.Errors.Add($"Report folder not exist for file {fileId}");
                return (null, null, NotFound(cloudProxyResponseModel));
            }

            string reportPath = Path.Combine(reportFolderPath, Constants.REPORT_XML_FILE_NAME);
            if (!System.IO.File.Exists(reportPath))
            {
                _logger.LogWarning($"[{UserAgentInfo.ClientTypeString}]:: Report xml not exist for file {fileId}");
                cloudProxyResponseModel.Errors.Add($"Report xml not exist for file {fileId}");
                return (null, null, NotFound(cloudProxyResponseModel));
            }

            byte[] reportXmlBytes = await System.IO.File.ReadAllBytesAsync(reportPath);
            string xmlReportFileText = System.Text.Encoding.UTF8.GetString(reportXmlBytes);
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: Report json {xmlReportFileText.XmlStringToJson()} for file {fileId}");
            return (reportXmlBytes, xmlReportFileText, null);
        }
    }
}
