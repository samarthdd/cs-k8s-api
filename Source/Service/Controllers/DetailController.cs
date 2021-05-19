using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class DetailController : CloudProxyController<DetailController>
    {
        public DetailController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<DetailController> logger, IFileUtility fileUtility,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration) : base(logger, adaptationServiceClient, fileUtility, cloudSdkConfiguration,
                                                                                        processingConfiguration, storeConfiguration, zipUtility)
        {
        }

        [HttpGet(Constants.Endpoints.VERSION)]
        public IActionResult GetVersionDetails()
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(GetVersionDetails)} method invoked");
            return Ok(new
            {
                _cloudSdkConfiguration.SDKEngineVersion,
                _cloudSdkConfiguration.SDKApiVersion
            });
        }
    }
}
