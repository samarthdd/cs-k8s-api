using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public class RabbitMqQueueConfiguration : IQueueConfiguration
    {
        private bool _disposedValue;

        public string MBUsername { get; set; }
        public string MBPassword { get; set; }
        public string MBHostName { get; set; }
        public int MBPort { get; set; }
        public string ExchangeName { get; set; }
        public string RequestQueueName { get; set; }
        public string OutcomeQueueName { get; set; }
        public string RequestMessageName { get; set; }
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
