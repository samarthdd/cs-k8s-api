using Glasswall.CloudProxy.Common;
using Glasswall.CloudProxy.Common.AdaptationService;
using Glasswall.CloudProxy.Common.Configuration;
using Glasswall.CloudProxy.Common.Utilities;
using Glasswall.CloudProxy.Common.Web.Abstraction;
using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class SDKController : CloudProxyController<SDKController>
    {
        public SDKController(IAdaptationServiceClient<AdaptationOutcomeProcessor> adaptationServiceClient, IStoreConfiguration storeConfiguration,
            IProcessingConfiguration processingConfiguration, ILogger<SDKController> logger, IFileUtility fileUtility,
            IZipUtility zipUtility, ICloudSdkConfiguration cloudSdkConfiguration) : base(logger, adaptationServiceClient, fileUtility, cloudSdkConfiguration,
                                                                                        processingConfiguration, storeConfiguration, zipUtility)
        {
        }

        [HttpPost(Constants.Endpoints.REBUILD_ZIP_FROM_FILE)]
        public async Task<IActionResult> RebuildFromFormFile([FromForm][Required] IFormFile file)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(RebuildFromFormFile)} method invoked");
            return await RebuildZipFile(formFile: file);
        }

        [HttpPost(Constants.Endpoints.REBUILD_ZIP_FROM_BASE64)]
        public async Task<IActionResult> RebuildFromBase64([FromBody][Required] Base64Request request)
        {
            _logger.LogInformation($"[{UserAgentInfo.ClientTypeString}]:: {nameof(RebuildFromBase64)} method invoked");
            return await RebuildZipFile(request);
        }
    }
}
