using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    public class CloudProxyResponseModel : ICloudProxyResponseModel
    {
        public CloudProxyResponseModel()
        {
            Errors = new List<string>();
        }

        public List<string> Errors { get; set; }

        public ReturnOutcome? Status { get; set; }
        public RebuildProcessingStatus? RebuildProcessingStatus { get; set; }
    }
}
