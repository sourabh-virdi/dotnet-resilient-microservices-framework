using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Trace;
using ResilientMicroservices.Core;
using System.Diagnostics;

namespace ResilientMicroservices.Tracing;

public class OpenTelemetryDistributedTracing : IDistributedTracing
{
    private readonly TracingOptions _options;
    private readonly ILogger<OpenTelemetryDistributedTracing> _logger;
    private readonly ActivitySource _activitySource;

    public OpenTelemetryDistributedTracing(
        IOptions<TracingOptions> options,
        ILogger<OpenTelemetryDistributedTracing> logger)
    {
        _options = options.Value;
        _logger = logger;
        _activitySource = new ActivitySource(_options.ServiceName, _options.ServiceVersion);
    }

    public Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        var activity = _activitySource.StartActivity(operationName, kind);
        
        if (activity != null)
        {
            activity.SetTag("service.name", _options.ServiceName);
            activity.SetTag("service.version", _options.ServiceVersion);
            
            if (!string.IsNullOrEmpty(_options.Environment))
            {
                activity.SetTag("environment", _options.Environment);
            }

            _logger.LogDebug("Started activity {OperationName} with trace ID {TraceId}", 
                operationName, activity.TraceId);
        }

        return activity;
    }

    public void AddTag(string key, string value)
    {
        Activity.Current?.SetTag(key, value);
        _logger.LogTrace("Added tag {Key}={Value} to current activity", key, value);
    }

    public void AddEvent(string name, DateTimeOffset timestamp = default, IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        if (Activity.Current != null)
        {
            var eventTimestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
            var activityEvent = tags != null 
                ? new ActivityEvent(name, eventTimestamp, new ActivityTagsCollection(tags))
                : new ActivityEvent(name, eventTimestamp);
                
            Activity.Current.AddEvent(activityEvent);
            _logger.LogTrace("Added event {EventName} to current activity", name);
        }
    }

    public void SetStatus(ActivityStatusCode statusCode, string? description = null)
    {
        if (Activity.Current != null)
        {
            Activity.Current.SetStatus(statusCode, description);
            _logger.LogTrace("Set activity status to {StatusCode}: {Description}", statusCode, description);
        }
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

public class TracingOptions
{
    public string ServiceName { get; set; } = "Unknown";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string? Environment { get; set; }
    public bool EnableConsoleExporter { get; set; } = true;
    public bool EnableJaegerExporter { get; set; } = true;
    public string JaegerEndpoint { get; set; } = "http://localhost:14268/api/traces";
    public double SamplingRatio { get; set; } = 1.0; // Sample all traces by default
} 