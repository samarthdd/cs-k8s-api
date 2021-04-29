using Glasswall.CloudProxy.Common.Configuration;

namespace Glasswall.CloudProxy.Common.ConfigLoaders
{
    public static class VersionConfigLoader
    {
        public static IVersionConfiguration SetDefaults(IVersionConfiguration configuration)
        {
            configuration.SDKEngineVersion = Constants.Header.SDK_ENGINE_VERSION_VALUE;
            configuration.SDKApiVersion = Constants.Header.SDK_API_VERSION_VALUE;
            return configuration;
        }
    }
}
