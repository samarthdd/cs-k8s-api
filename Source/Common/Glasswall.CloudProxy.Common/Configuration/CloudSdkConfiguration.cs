using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public class CloudSdkConfiguration : ICloudSdkConfiguration
    {
        private bool _disposedValue;

        public string SDKEngineVersion { get; set; }
        public string SDKApiVersion { get; set; }
        public bool EnableCache { get; set; }
        public int HstsMaxAgeInDays { get; set; }
        public bool HstsIncludeSubDomains { get; set; }
        public bool HstsPreload { get; set; }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
