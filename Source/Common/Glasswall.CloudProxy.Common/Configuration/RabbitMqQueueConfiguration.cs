namespace Glasswall.CloudProxy.Common.Configuration
{
    public class RabbitMqQueueConfiguration : IQueueConfiguration
    {
        public string MBUsername { get; set; }
        public string MBPassword { get; set; }
        public string MBHostName { get; set; }
        public int MBPort { get; set; }
        public string ExchangeName { get; set; }
        public string RequestQueueName { get; set; }
        public string OutcomeQueueName { get; set; }
        public string RequestMessageName { get; set; }
    }
}
