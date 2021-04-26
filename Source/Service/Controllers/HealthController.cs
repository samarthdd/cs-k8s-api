using Glasswall.CloudProxy.Common.Web.Abstraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Glasswall.CloudProxy.Api.Controllers
{
    public class HealthController : CloudProxyController<HealthController>
    {
        public HealthController(ILogger<HealthController> logger) : base(logger)
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
    }
}
