using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Core;

public interface IHealthCheckProvider
{
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    string Name { get; }
}

public class HealthCheckResult
{
    public HealthStatus Status { get; set; }
    public string? Description { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Data { get; set; }

    public static HealthCheckResult Healthy(string? description = null, Dictionary<string, object>? data = null)
        => new() { Status = HealthStatus.Healthy, Description = description, Data = data };

    public static HealthCheckResult Degraded(string? description = null, Exception? exception = null, Dictionary<string, object>? data = null)
        => new() { Status = HealthStatus.Degraded, Description = description, Exception = exception, Data = data };

    public static HealthCheckResult Unhealthy(string? description = null, Exception? exception = null, Dictionary<string, object>? data = null)
        => new() { Status = HealthStatus.Unhealthy, Description = description, Exception = exception, Data = data };
}

public enum HealthStatus
{
    Unhealthy = 0,
    Degraded = 1,
    Healthy = 2
} 