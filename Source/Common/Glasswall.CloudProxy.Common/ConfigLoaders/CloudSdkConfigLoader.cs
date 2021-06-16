using Glasswall.CloudProxy.Common.Configuration;

namespace Glasswall.CloudProxy.Common.ConfigLoaders
{
    public static class CloudSdkConfigLoader
    {
        public static ICloudSdkConfiguration SetDefaults(ICloudSdkConfiguration configuration)
        {
            configuration.SDKEngineVersion = Constants.Header.SDK_ENGINE_VERSION_VALUE;
            configuration.SDKApiVersion = Constants.Header.SDK_API_VERSION_VALUE;
            configuration.EnableCache = false;
            configuration.HstsMaxAgeInDays = Constants.Hsts.HSTS_MAX_AGE_IN_DAYS;
            configuration.HstsPreload = Constants.Hsts.HSTS_PRELOAD;
            configuration.HstsIncludeSubDomains = Constants.Hsts.HSTS_INCLUDE_SUBDOMAINS;
            return configuration;
        }
    }
}
