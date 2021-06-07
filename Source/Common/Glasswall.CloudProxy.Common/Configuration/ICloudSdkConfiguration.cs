using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface ICloudSdkConfiguration : IDisposable
    {
        public string SDKEngineVersion { get; set; }
        public string SDKApiVersion { get; set; }
        public bool EnableCache { get; set; }
    }
}
