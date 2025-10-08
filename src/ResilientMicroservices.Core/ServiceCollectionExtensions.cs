using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;

namespace ResilientMicroservices.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientMicroservicesCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IDistributedTracing, DefaultDistributedTracing>();
        services.TryAddSingleton<IMetricsCollector, NoOpMetricsCollector>();
        
        return services;
    }

    public static IServiceCollection AddHealthChecks(this IServiceCollection services, Action<HealthCheckBuilder>? configure = null)
    {
        var builder = new HealthCheckBuilder(services);
        configure?.Invoke(builder);
        
        return services;
    }
}

public class HealthCheckBuilder
{
    private readonly IServiceCollection _services;

    public HealthCheckBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public HealthCheckBuilder AddProvider<T>() where T : class, IHealthCheckProvider
    {
        _services.AddSingleton<IHealthCheckProvider, T>();
        return this;
    }

    public HealthCheckBuilder AddProvider<T>(T provider) where T : class, IHealthCheckProvider
    {
        _services.AddSingleton<IHealthCheckProvider>(provider);
        return this;
    }
}

// Default implementation for IDistributedTracing (will be improved in the Tracing library)
internal class DefaultDistributedTracing : IDistributedTracing
{
    public Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return Activity.Current?.Source.StartActivity(operationName, kind);
    }

    public void AddTag(string key, string value)
    {
        Activity.Current?.SetTag(key, value);
    }

    public void AddEvent(string name, DateTimeOffset timestamp = default, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        Activity.Current?.AddEvent(new ActivityEvent(name, timestamp, new ActivityTagsCollection(tags)));
    }

    public void SetStatus(ActivityStatusCode statusCode, string? description = null)
    {
        Activity.Current?.SetStatus(statusCode, description);
    }

    public string? GetTraceId()
    {
        return Activity.Current?.TraceId.ToString();
    }

    public string? GetSpanId()
    {
        return Activity.Current?.SpanId.ToString();
    }
}

// Default no-op implementation for IMetricsCollector
internal class NoOpMetricsCollector : IMetricsCollector
{
    public void IncrementCounter(string name, long value = 1, params KeyValuePair<string, object?>[] tags) { }

    public void RecordValue(string name, double value, params KeyValuePair<string, object?>[] tags) { }

    public void SetGauge(string name, double value, params KeyValuePair<string, object?>[] tags) { }

    public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration) { }

    public void RecordCircuitBreakerStateChange(string circuitBreakerName, string fromState, string toState) { }

    public void RecordRetryAttempt(string operation, int attemptNumber, bool isSuccessful) { }

    public void RecordSagaExecution(string sagaName, string result, TimeSpan duration, int stepCount) { }

    public void RecordMessageOperation(string operation, string messageType, bool isSuccessful, TimeSpan? duration = null) { }
} 