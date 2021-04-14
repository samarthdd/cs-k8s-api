using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    public interface ICloudProxyResponseModel
    {
        List<string> Errors { get; set; }
        ReturnOutcome? Status { get; set; }
    }
}
