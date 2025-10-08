using System.Diagnostics.Metrics;

namespace ResilientMicroservices.Core;

public interface IMetricsCollector
{
    /// <summary>
    /// Increments a counter metric
    /// </summary>
    void IncrementCounter(string name, long value = 1, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records a histogram value (for measuring distributions like request duration)
    /// </summary>
    void RecordValue(string name, double value, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Sets a gauge value (for measuring current state like active connections)
    /// </summary>
    void SetGauge(string name, double value, params KeyValuePair<string, object?>[] tags);

    /// <summary>
    /// Records request duration and outcome
    /// </summary>
    void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration);

    /// <summary>
    /// Records circuit breaker state changes
    /// </summary>
    void RecordCircuitBreakerStateChange(string circuitBreakerName, string fromState, string toState);

    /// <summary>
    /// Records retry attempts
    /// </summary>
    void RecordRetryAttempt(string operation, int attemptNumber, bool isSuccessful);

    /// <summary>
    /// Records saga execution metrics
    /// </summary>
    void RecordSagaExecution(string sagaName, string result, TimeSpan duration, int stepCount);

    /// <summary>
    /// Records message bus metrics
    /// </summary>
    void RecordMessageOperation(string operation, string messageType, bool isSuccessful, TimeSpan? duration = null);
}

public static class MetricNames
{
    // HTTP Metrics
    public const string HttpRequestDuration = "http_request_duration_ms";
    public const string HttpRequestsTotal = "http_requests_total";
    
    // Circuit Breaker Metrics
    public const string CircuitBreakerStateChanges = "circuit_breaker_state_changes_total";
    public const string CircuitBreakerCurrentState = "circuit_breaker_current_state";
    
    // Retry Metrics
    public const string RetryAttemptsTotal = "retry_attempts_total";
    public const string RetryDuration = "retry_duration_ms";
    
    // Saga Metrics
    public const string SagaExecutionDuration = "saga_execution_duration_ms";
    public const string SagaExecutionsTotal = "saga_executions_total";
    public const string SagaStepExecutionDuration = "saga_step_execution_duration_ms";
    
    // Message Bus Metrics
    public const string MessageOperationsTotal = "message_operations_total";
    public const string MessageOperationDuration = "message_operation_duration_ms";
    
    // Health Check Metrics
    public const string HealthCheckDuration = "health_check_duration_ms";
    public const string HealthCheckStatus = "health_check_status";
    
    // System Metrics
    public const string ActiveConnections = "active_connections";
    public const string MemoryUsage = "memory_usage_bytes";
    public const string CpuUsage = "cpu_usage_percent";
}

public static class MetricTags
{
    public const string Method = "method";
    public const string Endpoint = "endpoint";
    public const string StatusCode = "status_code";
    public const string CircuitBreakerName = "circuit_breaker_name";
    public const string FromState = "from_state";
    public const string ToState = "to_state";
    public const string Operation = "operation";
    public const string AttemptNumber = "attempt_number";
    public const string IsSuccessful = "is_successful";
    public const string SagaName = "saga_name";
    public const string Result = "result";
    public const string StepCount = "step_count";
    public const string MessageType = "message_type";
    public const string HealthCheckName = "health_check_name";
    public const string Status = "status";
} 