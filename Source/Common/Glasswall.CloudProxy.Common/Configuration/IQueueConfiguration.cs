using System;

namespace Glasswall.CloudProxy.Common.Configuration
{
    public interface IQueueConfiguration : IDisposable
    {
        string MBUsername { get; set; }
        string MBPassword { get; set; }

        string MBHostName { get; set; }
        int MBPort { get; set; }
        string ExchangeName { get; set; }

        string RequestQueueName { get; set; }
        string OutcomeQueueName { get; set; }

        string RequestMessageName { get; set; }

    }
}
