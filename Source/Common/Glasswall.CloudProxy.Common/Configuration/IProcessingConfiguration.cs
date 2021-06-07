using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface IProcessingConfiguration : IDisposable
    {
        TimeSpan ProcessingTimeoutDuration { get; set; }
    }
}
