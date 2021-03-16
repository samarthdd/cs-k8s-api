using Glasswall.CloudProxy.Common;
using System.Collections.Generic;

namespace Glasswall.CloudProxy.Api.Models
{
    public interface ICloudProxyResponseModel
    {
        List<string> Errors { get; set; }
        ReturnOutcome? Status { get; set; }
    }
}
