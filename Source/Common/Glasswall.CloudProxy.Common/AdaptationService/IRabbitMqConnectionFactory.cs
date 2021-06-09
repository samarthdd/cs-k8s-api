using RabbitMQ.Client;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public interface IRabbitMqConnectionFactory
    {
        public IConnection Connection { get; }
    }
}
