using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public class IcapProcessingConfiguration : IProcessingConfiguration
    {
        public TimeSpan ProcessingTimeoutDuration { get; set; }
    }
}
