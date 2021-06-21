using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public class IcapProcessingConfiguration : IProcessingConfiguration
    {
        private bool _disposedValue;

        public TimeSpan ProcessingTimeoutDuration { get; set; }
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
