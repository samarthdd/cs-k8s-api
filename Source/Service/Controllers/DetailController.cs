using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class DetailController : CloudProxyController<DetailController>
    {
        private readonly ICloudSdkConfiguration _versionConfiguration;
        public DetailController(ILogger<DetailController> logger, ICloudSdkConfiguration versionConfiguration) : base(logger)
        {
            _versionConfiguration = versionConfiguration ?? throw new ArgumentNullException(nameof(versionConfiguration));
        }

        [HttpGet(Constants.Endpoints.VERSION)]
        public IActionResult GetVersionDetails()
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(GetVersionDetails)} method invoked");
            return Ok(new
            {
                _versionConfiguration.SDKEngineVersion,
                _versionConfiguration.SDKApiVersion
            });
        }
    }
}
