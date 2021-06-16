using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface ICloudSdkConfiguration : IDisposable
    {
        public string SDKEngineVersion { get; set; }
        public string SDKApiVersion { get; set; }
        public bool EnableCache { get; set; }
        public int HstsMaxAgeInDays { get; set; }
        public bool HstsIncludeSubDomains { get; set; }
        public bool HstsPreload { get; set; }
    }
}
