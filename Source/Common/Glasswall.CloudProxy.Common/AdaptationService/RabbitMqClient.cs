using Glasswall.CloudProxy.Common.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class RabbitMqClient<TResponseProcessor> : IAdaptationServiceClient<TResponseProcessor> where TResponseProcessor : IResponseProcessor
    {
        private readonly IConnectionFactory _connectionFactory;
        private IConnection _connection;
        private IModel _channel;
        private EventingBasicConsumer _consumer;
        private bool _disposedValue;
        private readonly BlockingCollection<IAdaptationServiceResponse> _respQueue = new BlockingCollection<IAdaptationServiceResponse>();
        private readonly IResponseProcessor _responseProcessor;
        private readonly IQueueConfiguration _queueConfiguration;
        private readonly ILogger<RabbitMqClient<TResponseProcessor>> _logger;

        public RabbitMqClient(IResponseProcessor responseProcessor, IQueueConfiguration queueConfiguration, ILogger<RabbitMqClient<TResponseProcessor>> logger)
        {
            _responseProcessor = responseProcessor ?? throw new ArgumentNullException(nameof(responseProcessor));
            _queueConfiguration = queueConfiguration ?? throw new ArgumentNullException(nameof(queueConfiguration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CheckCredentials(queueConfiguration);

            _logger.LogInformation($"Setting up queue connection '{queueConfiguration.MBHostName}:{queueConfiguration.MBPort}'");
            _connectionFactory = new ConnectionFactory()
            {
                HostName = queueConfiguration.MBHostName,
                Port = queueConfiguration.MBPort,
                UserName = queueConfiguration.MBUsername,
                Password = queueConfiguration.MBPassword
            };
        }

        private void CheckCredentials(IQueueConfiguration queueConfiguration)
        {
            if (string.IsNullOrEmpty(queueConfiguration.MBUsername))
            {
                queueConfiguration.MBUsername = ConnectionFactory.DefaultUser;
                _logger.LogInformation("No RabbitMQ Username provided, using default");
            }
            if (string.IsNullOrEmpty(queueConfiguration.MBPassword))
            {
                queueConfiguration.MBPassword = ConnectionFactory.DefaultPass;
                _logger.LogInformation("No RabbitMQ Password provided, using default");
            }
        }

        public void Connect()
        {
            if (_connection != null || _channel != null || _consumer != null)
            {
                throw new AdaptationServiceClientException("'Connect' should only be called once.");
            }

            _connection = _connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _consumer = new EventingBasicConsumer(_channel);

            _consumer.Received += (model, ea) =>
            {
                try
                {
                    _logger.LogInformation($"Received message: Exchange Name: '{ea.Exchange}', Routing Key: '{ea.RoutingKey}'");
                    IDictionary<string, object> headers = ea.BasicProperties.Headers;
                    byte[] body = ea.Body.ToArray();

                    _respQueue.Add(_responseProcessor.Process(headers, body));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error Processing 'input'");
                    _respQueue.Add(new AdaptationServiceResponse { FileOutcome = ReturnOutcome.GW_ERROR });
                }
            };
        }

        public IAdaptationServiceResponse AdaptationRequest(Guid fileId, string originalStoreFilePath, string rebuiltStoreFilePath, CancellationToken processingCancellationToken)
        {
            if (_connection == null || _channel == null || _consumer == null)
            {
                throw new AdaptationServiceClientException("'Connect' should be called before 'AdaptationRequest'.");
            }

            QueueDeclareOk queueDeclare = _channel.QueueDeclare(queue: _queueConfiguration.RequestQueueName,
                                                          durable: false,
                                                          exclusive: false,
                                                          autoDelete: false,
                                                          arguments: null);
            _logger.LogInformation($"Send Request Queue '{queueDeclare.QueueName}' Declared : MessageCount = {queueDeclare.MessageCount},  ConsumerCount = {queueDeclare.ConsumerCount}");

            IDictionary<string, object> headerMap = new Dictionary<string, object>
                    {
                        { Constants.Header.ICAP_FILE_ID, fileId.ToString() },
                        { Constants.Header.ICAP_REQUEST_MODE, Constants.Header.ICAP_REQUEST_MODE_VALUE },
                        { Constants.Header.ICAP_SOURCE_FILE_LOCATION, originalStoreFilePath},
                        { Constants.Header.ICAP_REBUILT_FILE_LOCATION, rebuiltStoreFilePath},
                        { Constants.Header.ICAP_GENERATE_REPORT, "true"}
                    };

            IBasicProperties messageProperties = _channel.CreateBasicProperties();
            messageProperties.Headers = headerMap;
            messageProperties.ReplyTo = _queueConfiguration.OutcomeQueueName;

            _logger.LogInformation($"Sending {_queueConfiguration.RequestMessageName} for {fileId}");

            _channel.BasicConsume(_consumer, _queueConfiguration.OutcomeQueueName, autoAck: true);

            _channel.BasicPublish(exchange: _queueConfiguration.ExchangeName,
                                 routingKey: _queueConfiguration.RequestMessageName,
                                 basicProperties: messageProperties);

            return _respQueue.Take(processingCancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _channel?.Dispose();
                    _connection?.Dispose();
                    _respQueue?.Dispose();
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
