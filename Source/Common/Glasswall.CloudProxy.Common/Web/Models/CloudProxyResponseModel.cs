using System;
using System.Collections.Generic;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    public class CloudProxyResponseModel : ICloudProxyResponseModel
    {
        private bool _disposedValue;

        public CloudProxyResponseModel()
        {
            Errors = new List<string>();
        }

        public List<string> Errors { get; set; }

        public ReturnOutcome? Status { get; set; }
        public RebuildProcessingStatus? RebuildProcessingStatus { get; set; }
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
