using System;
using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    public interface ICloudProxyResponseModel : IDisposable
    {
        List<string> Errors { get; set; }
        ReturnOutcome? Status { get; set; }
        RebuildProcessingStatus? RebuildProcessingStatus { get; set; }
    }
}
