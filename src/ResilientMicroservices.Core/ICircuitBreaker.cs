using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Core;

public interface ICircuitBreaker
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
    CircuitBreakerState State { get; }
    string Name { get; }
}

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int SamplingDuration { get; set; } = 10;
    public int MinimumThroughput { get; set; } = 3;
} 