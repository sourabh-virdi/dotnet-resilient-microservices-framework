using Microsoft.Extensions.Logging;
using ResilientMicroservices.Core;
using System.Diagnostics.Metrics;

namespace ResilientMicroservices.Metrics;

public class DiagnosticsMetricsCollector : IMetricsCollector, IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<DiagnosticsMetricsCollector> _logger;

    // Counters
    private readonly Counter<long> _httpRequestsCounter;
    private readonly Counter<long> _circuitBreakerStateChangesCounter;
    private readonly Counter<long> _retryAttemptsCounter;
    private readonly Counter<long> _sagaExecutionsCounter;
    private readonly Counter<long> _messageOperationsCounter;

    // Histograms
    private readonly Histogram<double> _httpRequestDurationHistogram;
    private readonly Histogram<double> _retryDurationHistogram;
    private readonly Histogram<double> _sagaExecutionDurationHistogram;
    private readonly Histogram<double> _sagaStepExecutionDurationHistogram;
    private readonly Histogram<double> _messageOperationDurationHistogram;
    private readonly Histogram<double> _healthCheckDurationHistogram;

    // Observable Gauges
    private readonly ObservableGauge<double> _memoryUsageGauge;
    private readonly ObservableGauge<double> _cpuUsageGauge;
    private readonly ObservableGauge<long> _activeConnectionsGauge;

    private double _currentMemoryUsage;
    private double _currentCpuUsage;
    private long _currentActiveConnections;

    public DiagnosticsMetricsCollector(ILogger<DiagnosticsMetricsCollector> logger)
    {
        _logger = logger;
        _meter = new Meter("ResilientMicroservices", "1.0.0");

        // Initialize counters
        _httpRequestsCounter = _meter.CreateCounter<long>(
            MetricNames.HttpRequestsTotal,
            description: "Total number of HTTP requests");

        _circuitBreakerStateChangesCounter = _meter.CreateCounter<long>(
            MetricNames.CircuitBreakerStateChanges,
            description: "Total number of circuit breaker state changes");

        _retryAttemptsCounter = _meter.CreateCounter<long>(
            MetricNames.RetryAttemptsTotal,
            description: "Total number of retry attempts");

        _sagaExecutionsCounter = _meter.CreateCounter<long>(
            MetricNames.SagaExecutionsTotal,
            description: "Total number of saga executions");

        _messageOperationsCounter = _meter.CreateCounter<long>(
            MetricNames.MessageOperationsTotal,
            description: "Total number of message operations");

        // Initialize histograms
        _httpRequestDurationHistogram = _meter.CreateHistogram<double>(
            MetricNames.HttpRequestDuration,
            unit: "ms",
            description: "Duration of HTTP requests");

        _retryDurationHistogram = _meter.CreateHistogram<double>(
            MetricNames.RetryDuration,
            unit: "ms",
            description: "Duration of retry operations");

        _sagaExecutionDurationHistogram = _meter.CreateHistogram<double>(
            MetricNames.SagaExecutionDuration,
            unit: "ms",
            description: "Duration of saga executions");

        _sagaStepExecutionDurationHistogram = _meter.CreateHistogram<double>(
            MetricNames.SagaStepExecutionDuration,
            unit: "ms",
            description: "Duration of individual saga steps");

        _messageOperationDurationHistogram = _meter.CreateHistogram<double>(
            MetricNames.MessageOperationDuration,
            unit: "ms",
            description: "Duration of message operations");

        _healthCheckDurationHistogram = _meter.CreateHistogram<double>(
            MetricNames.HealthCheckDuration,
            unit: "ms",
            description: "Duration of health checks");

        // Initialize observable gauges
        _memoryUsageGauge = _meter.CreateObservableGauge<double>(
            MetricNames.MemoryUsage,
            observeValue: () => _currentMemoryUsage,
            unit: "bytes",
            description: "Current memory usage");

        _cpuUsageGauge = _meter.CreateObservableGauge<double>(
            MetricNames.CpuUsage,
            observeValue: () => _currentCpuUsage,
            unit: "%",
            description: "Current CPU usage percentage");

        _activeConnectionsGauge = _meter.CreateObservableGauge<long>(
            MetricNames.ActiveConnections,
            observeValue: () => _currentActiveConnections,
            description: "Number of active connections");

        _logger.LogInformation("DiagnosticsMetricsCollector initialized with meter: {MeterName}", _meter.Name);
    }

    public void IncrementCounter(string name, long value = 1, params KeyValuePair<string, object?>[] tags)
    {
        try
        {
            var counter = _meter.CreateCounter<long>(name);
            counter.Add(value, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment counter {CounterName}", name);
        }
    }

    public void RecordValue(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        try
        {
            var histogram = _meter.CreateHistogram<double>(name);
            histogram.Record(value, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record value for histogram {HistogramName}", name);
        }
    }

    public void SetGauge(string name, double value, params KeyValuePair<string, object?>[] tags)
    {
        try
        {
            // For dynamic gauges, we'll use a different approach
            // Store the value based on the gauge name
            switch (name)
            {
                case MetricNames.MemoryUsage:
                    _currentMemoryUsage = value;
                    break;
                case MetricNames.CpuUsage:
                    _currentCpuUsage = value;
                    break;
                case MetricNames.ActiveConnections:
                    _currentActiveConnections = (long)value;
                    break;
                default:
                    _logger.LogWarning("Unknown gauge name: {GaugeName}", name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set gauge {GaugeName}", name);
        }
    }

    public void RecordHttpRequest(string method, string endpoint, int statusCode, TimeSpan duration)
    {
        try
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new(MetricTags.Method, method),
                new(MetricTags.Endpoint, endpoint),
                new(MetricTags.StatusCode, statusCode.ToString())
            };

            _httpRequestsCounter.Add(1, tags);
            _httpRequestDurationHistogram.Record(duration.TotalMilliseconds, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record HTTP request metrics for {Method} {Endpoint}", method, endpoint);
        }
    }

    public void RecordCircuitBreakerStateChange(string circuitBreakerName, string fromState, string toState)
    {
        try
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new(MetricTags.CircuitBreakerName, circuitBreakerName),
                new(MetricTags.FromState, fromState),
                new(MetricTags.ToState, toState)
            };

            _circuitBreakerStateChangesCounter.Add(1, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record circuit breaker state change for {CircuitBreakerName}", circuitBreakerName);
        }
    }

    public void RecordRetryAttempt(string operation, int attemptNumber, bool isSuccessful)
    {
        try
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new(MetricTags.Operation, operation),
                new(MetricTags.AttemptNumber, attemptNumber.ToString()),
                new(MetricTags.IsSuccessful, isSuccessful.ToString())
            };

            _retryAttemptsCounter.Add(1, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record retry attempt for operation {Operation}", operation);
        }
    }

    public void RecordSagaExecution(string sagaName, string result, TimeSpan duration, int stepCount)
    {
        try
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new(MetricTags.SagaName, sagaName),
                new(MetricTags.Result, result),
                new(MetricTags.StepCount, stepCount.ToString())
            };

            _sagaExecutionsCounter.Add(1, tags);
            _sagaExecutionDurationHistogram.Record(duration.TotalMilliseconds, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record saga execution metrics for {SagaName}", sagaName);
        }
    }

    public void RecordMessageOperation(string operation, string messageType, bool isSuccessful, TimeSpan? duration = null)
    {
        try
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new(MetricTags.Operation, operation),
                new(MetricTags.MessageType, messageType),
                new(MetricTags.IsSuccessful, isSuccessful.ToString())
            };

            _messageOperationsCounter.Add(1, tags);

            if (duration.HasValue)
            {
                _messageOperationDurationHistogram.Record(duration.Value.TotalMilliseconds, tags);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record message operation metrics for {Operation} {MessageType}", operation, messageType);
        }
    }

    public void RecordHealthCheck(string healthCheckName, string status, TimeSpan duration)
    {
        try
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new(MetricTags.HealthCheckName, healthCheckName),
                new(MetricTags.Status, status)
            };

            _healthCheckDurationHistogram.Record(duration.TotalMilliseconds, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record health check metrics for {HealthCheckName}", healthCheckName);
        }
    }

    public void UpdateSystemMetrics()
    {
        try
        {
            // Update memory usage
            var memoryUsage = GC.GetTotalMemory(false);
            SetGauge(MetricNames.MemoryUsage, memoryUsage);

            // CPU usage would require more complex calculation or external library
            // For now, we'll leave it as a placeholder
            // SetGauge(MetricNames.CpuUsage, GetCpuUsage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update system metrics");
        }
    }

    public void Dispose()
    {
        _meter?.Dispose();
        _logger.LogInformation("DiagnosticsMetricsCollector disposed");
    }
} 