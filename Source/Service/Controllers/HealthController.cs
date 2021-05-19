using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class HealthController : CloudProxyController<HealthController>
    {
        public HealthController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<HealthController> logger, IFileUtility fileUtility,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration) : base(logger, adaptationServiceClient, fileUtility, cloudSdkConfiguration,
                                                                                        processingConfiguration, storeConfiguration, zipUtility)
        {
        }

        [HttpGet]
        public IActionResult GetHealth()
        {
            _logger.Log(LogLevel.Trace, $"[{UserAgentInfo.ClientTypeString}]:: Performing heartbeat");
            return Ok(new
            {
                Status = HttpStatusCode.OK
            });
        }

        [HttpGet]
        [Route(Constants.Endpoints.Default)]
        public IActionResult Swagger()
        {
            return Redirect(Constants.SWAGGER_URL);
        }
    }
}
