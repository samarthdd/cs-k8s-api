using Glasswall.CloudProxy.Common.Configuration;
using System;

namespace Glasswall.CloudProxy.Common.ConfigLoaders
{
    public static class IcapProcessingConfigLoader
    {
        public static IProcessingConfiguration SetDefaults(IProcessingConfiguration configuration)
        {
            configuration.ProcessingTimeoutDuration = TimeSpan.FromSeconds(60);
            return configuration;
        }
    }
}
