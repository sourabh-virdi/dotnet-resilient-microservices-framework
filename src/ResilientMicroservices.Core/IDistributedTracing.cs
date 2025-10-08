using System.Diagnostics;
using System.Threading.Tasks;

namespace ResilientMicroservices.Core;

public interface IDistributedTracing
{
    Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal);
    void AddTag(string key, string value);
    void AddEvent(string name, DateTimeOffset timestamp = default, IEnumerable<KeyValuePair<string, object?>>? tags = null);
    void SetStatus(ActivityStatusCode statusCode, string? description = null);
    string? GetTraceId();
    string? GetSpanId();
}

public interface ITracingContext
{
    string? TraceId { get; }
    string? SpanId { get; }
    string? ParentSpanId { get; }
    Dictionary<string, string> Baggage { get; }
}

public class TracingMetrics
{
    public string ServiceName { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
} 