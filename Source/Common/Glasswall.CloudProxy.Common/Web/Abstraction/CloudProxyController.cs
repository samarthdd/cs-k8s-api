using Amazon.S3.Util;
using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

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
            _logger = logger;
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
    }
}
