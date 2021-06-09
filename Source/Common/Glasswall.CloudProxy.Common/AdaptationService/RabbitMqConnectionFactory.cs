using Glasswall.CloudProxy.Common.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class RabbitMqConnectionFactory : IRabbitMqConnectionFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<IRabbitMqConnectionFactory> _logger;
        private readonly IQueueConfiguration _queueConfiguration;
        private IConnection _connection;

        public RabbitMqConnectionFactory(IQueueConfiguration queueConfiguration, ILogger<IRabbitMqConnectionFactory> logger)
        {
            _queueConfiguration = queueConfiguration ?? throw new ArgumentNullException(nameof(queueConfiguration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CheckCredentials();

            _logger.LogInformation($"Setting up queue connection '{queueConfiguration.MBHostName}:{queueConfiguration.MBPort}'");
            _connectionFactory = new ConnectionFactory()
            {
                HostName = _queueConfiguration.MBHostName,
                Port = _queueConfiguration.MBPort,
                UserName = _queueConfiguration.MBUsername,
                Password = _queueConfiguration.MBPassword
            };
        }

        public IConnection Connection
        {
            get
            {
                if (_connection == null || !_connection.IsOpen)
                {
                    _connection = _connectionFactory.CreateConnection();
                }

                return _connection;
            }
        }

        private void CheckCredentials()
        {
            if (string.IsNullOrEmpty(_queueConfiguration.MBUsername))
            {
                _queueConfiguration.MBUsername = ConnectionFactory.DefaultUser;
                _logger.LogInformation("No RabbitMQ Username provided, using default");
            }
            if (string.IsNullOrEmpty(_queueConfiguration.MBPassword))
            {
                _queueConfiguration.MBPassword = ConnectionFactory.DefaultPass;
                _logger.LogInformation("No RabbitMQ Password provided, using default");
            }
        }
    }
}
