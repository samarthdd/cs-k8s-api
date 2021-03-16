using Glasswall.CloudProxy.Common;
using System.Collections.Generic;

namespace Glasswall.CloudProxy.Api.Models
{
    public class CloudProxyResponseModel : ICloudProxyResponseModel
    {
        public CloudProxyResponseModel()
        {
            Errors = new List<string>();
        }

        public List<string> Errors { get; set; }

        public ReturnOutcome? Status { get; set; }
    }
}
