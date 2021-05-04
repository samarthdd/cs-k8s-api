namespace Glasswall.CloudProxy.Common.Configuration
{
    public class CloudSdkConfiguration : ICloudSdkConfiguration
    {
        public string SDKEngineVersion { get; set; }
        public string SDKApiVersion { get; set; }
        public bool EnableCache { get; set; }
    }
}
