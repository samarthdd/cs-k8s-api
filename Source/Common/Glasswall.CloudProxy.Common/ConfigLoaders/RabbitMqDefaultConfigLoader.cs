
using Glasswall.CloudProxy.Common.Configuration;

namespace Glasswall.CloudProxy.Common.ConfigLoaders
{
    public static class RabbitMqDefaultConfigLoader
    {
        public static IQueueConfiguration SetDefaults(IQueueConfiguration configuration)
        {
            configuration.ExchangeName = "adaptation-exchange";
            configuration.RequestQueueName = "adaptation-request-queue";
            configuration.OutcomeQueueName = "amq.rabbitmq.reply-to";
            configuration.RequestMessageName = "adaptation-request";

            configuration.MBHostName = "rabbitmq-service";
            configuration.MBPort = 5672;
            return configuration;
        }
    }
}
