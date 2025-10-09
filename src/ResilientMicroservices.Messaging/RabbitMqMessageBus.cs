using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ResilientMicroservices.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Messaging;

public class RabbitMqMessageBus : IMessageBus, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessageBus> _logger;
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests;
    private readonly string _replyQueueName;
    private bool _disposed;

    public RabbitMqMessageBus(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessageBus> logger)
    {
        _options = options.Value;
        _logger = logger;
        _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection(_options.ConnectionName);
            _channel = _connection.CreateModel();

            // Setup reply queue for request-response pattern
            _replyQueueName = _channel.QueueDeclare().QueueName;
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += OnReplyReceived;
            _channel.BasicConsume(queue: _replyQueueName, autoAck: true, consumer: consumer);

            _logger.LogInformation("Connected to RabbitMQ at {HostName}:{Port}", _options.HostName, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default) where T : class
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel is not available");

        var exchangeName = _options.DefaultExchange;
        var messageRoutingKey = routingKey ?? typeof(T).Name.ToLowerInvariant();

        try
        {
            // Ensure exchange exists
            _channel.ExchangeDeclare(exchange: exchangeName, type: ExchangeType.Topic, durable: true);

            var messageWrapper = new MessageWrapper<T>
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                MessageType = typeof(T).FullName,
                CorrelationId = Activity.Current?.Id,
                Payload = message
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageWrapper));

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = messageWrapper.Id;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.Type = messageWrapper.MessageType;

            if (!string.IsNullOrEmpty(messageWrapper.CorrelationId))
            {
                properties.CorrelationId = messageWrapper.CorrelationId;
            }

            _channel.BasicPublish(
                exchange: exchangeName,
                routingKey: messageRoutingKey,
                basicProperties: properties,
                body: body);

            _logger.LogDebug("Published message {MessageId} of type {MessageType} with routing key {RoutingKey}",
                messageWrapper.Id, typeof(T).Name, messageRoutingKey);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<IDisposable> SubscribeAsync<T>(Func<T, Task> handler, string? routingKey = null, CancellationToken cancellationToken = default) where T : class
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel is not available");

        var exchangeName = _options.DefaultExchange;
        var messageRoutingKey = routingKey ?? typeof(T).Name.ToLowerInvariant();
        var queueName = $"{_options.ServiceName}.{typeof(T).Name}";

        try
        {
            // Ensure exchange exists
            _channel.ExchangeDeclare(exchange: exchangeName, type: ExchangeType.Topic, durable: true);

            // Declare queue
            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            // Bind queue to exchange
            _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: messageRoutingKey);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);
                    var messageWrapper = JsonSerializer.Deserialize<MessageWrapper<T>>(messageJson);

                    if (messageWrapper?.Payload != null)
                    {
                        _logger.LogDebug("Received message {MessageId} of type {MessageType}",
                            messageWrapper.Id, typeof(T).Name);

                        await handler(messageWrapper.Payload);
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        _logger.LogWarning("Received invalid message, rejecting");
                        _channel.BasicReject(deliveryTag: ea.DeliveryTag, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    _channel.BasicReject(deliveryTag: ea.DeliveryTag, requeue: true);
                }
            };

            var consumerTag = _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("Subscribed to messages of type {MessageType} with routing key {RoutingKey}",
                typeof(T).Name, messageRoutingKey);

            return new Subscription(_channel, consumerTag, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to messages of type {MessageType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, string? routingKey = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel is not available");

        var exchangeName = _options.DefaultExchange;
        var messageRoutingKey = routingKey ?? typeof(TRequest).Name.ToLowerInvariant();
        var requestTimeout = timeout ?? TimeSpan.FromSeconds(30);

        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>();
        
        _pendingRequests[correlationId] = tcs;

        try
        {
            var messageWrapper = new MessageWrapper<TRequest>
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                MessageType = typeof(TRequest).FullName,
                CorrelationId = correlationId,
                Payload = request
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageWrapper));

            var properties = _channel.CreateBasicProperties();
            properties.CorrelationId = correlationId;
            properties.ReplyTo = _replyQueueName;
            properties.MessageId = messageWrapper.Id;
            properties.Type = messageWrapper.MessageType;

            _channel.BasicPublish(
                exchange: exchangeName,
                routingKey: messageRoutingKey,
                basicProperties: properties,
                body: body);

            using var timeoutCts = new CancellationTokenSource(requestTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var responseJson = await tcs.Task.WaitAsync(combinedCts.Token);
            var responseWrapper = JsonSerializer.Deserialize<MessageWrapper<TResponse>>(responseJson);

            if (responseWrapper?.Payload == null)
                throw new InvalidOperationException("Received invalid response");

            return responseWrapper.Payload;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request timed out after {Timeout}", requestTimeout);
            throw new TimeoutException($"Request timed out after {requestTimeout}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send request of type {RequestType}", typeof(TRequest).Name);
            throw;
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    private void OnReplyReceived(object? sender, BasicDeliverEventArgs e)
    {
        var correlationId = e.BasicProperties.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId) && _pendingRequests.TryRemove(correlationId, out var tcs))
        {
            var responseJson = Encoding.UTF8.GetString(e.Body.ToArray());
            tcs.SetResult(responseJson);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _channel?.Close();
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ connection");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}

public class MessageWrapper<T> : BaseMessage
{
    public T? Payload { get; set; }
}

public class Subscription : IDisposable
{
    private readonly IModel _channel;
    private readonly string _consumerTag;
    private readonly ILogger _logger;

    public Subscription(IModel channel, string consumerTag, ILogger logger)
    {
        _channel = channel;
        _consumerTag = consumerTag;
        _logger = logger;
    }

    public void Dispose()
    {
        try
        {
            _channel.BasicCancel(_consumerTag);
            _logger.LogInformation("Cancelled subscription {ConsumerTag}", _consumerTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription {ConsumerTag}", _consumerTag);
        }
    }
}

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string DefaultExchange { get; set; } = "microservices.events";
    public string ServiceName { get; set; } = "unknown";
    public string ConnectionName { get; set; } = "ResilientMicroservices";
}
 