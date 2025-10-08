using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Core;

public interface IMessageBus
{
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken cancellationToken = default) where T : class;
    Task<IDisposable> SubscribeAsync<T>(Func<T, Task> handler, string? routingKey = null, CancellationToken cancellationToken = default) where T : class;
    Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, string? routingKey = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) 
        where TRequest : class 
        where TResponse : class;
}

public interface IMessage
{
    string Id { get; }
    DateTime Timestamp { get; }
    string? CorrelationId { get; }
    string? MessageType { get; }
}

public abstract class BaseMessage : IMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public string? MessageType { get; set; }
} 