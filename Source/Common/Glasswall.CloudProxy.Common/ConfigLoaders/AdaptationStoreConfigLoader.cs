
using Glasswall.CloudProxy.Common.Configuration;

namespace Glasswall.CloudProxy.Common.ConfigLoaders
{
    public static class AdaptationStoreConfigLoader
    {
        public static IStoreConfiguration SetDefaults(IStoreConfiguration configuration)
        {
            configuration.OriginalStorePath = "/var/source";
            configuration.RebuiltStorePath = "/var/target";

            return configuration;
        }
    }
}
