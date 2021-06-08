using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public class AdaptationStoreConfiguration : IStoreConfiguration
    {
        private bool _disposedValue;

        public string OriginalStorePath { get; set; }
        public string RebuiltStorePath { get; set; }
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
