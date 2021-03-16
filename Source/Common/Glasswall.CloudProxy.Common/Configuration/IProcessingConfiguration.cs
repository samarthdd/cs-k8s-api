using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface IProcessingConfiguration
    {
        TimeSpan ProcessingTimeoutDuration { get; set; }
    }
}
