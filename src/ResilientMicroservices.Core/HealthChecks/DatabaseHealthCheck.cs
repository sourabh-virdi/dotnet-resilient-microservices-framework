using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Core.HealthChecks;

public class DatabaseHealthCheck : IHealthCheckProvider
{
    private readonly DbConnection _connection;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public string Name => "Database";

    public DatabaseHealthCheck(DbConnection connection, ILogger<DatabaseHealthCheck> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await _connection.OpenAsync(cancellationToken);
            
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            
            stopwatch.Stop();
            
            _logger.LogDebug("Database health check succeeded in {Duration}ms", stopwatch.ElapsedMilliseconds);
            
            return HealthCheckResult.Healthy(
                "Database connection successful", 
                new Dictionary<string, object>
                {
                    ["duration_ms"] = stopwatch.ElapsedMilliseconds,
                    ["connection_state"] = _connection.State.ToString()
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Database health check failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
            
            return HealthCheckResult.Unhealthy(
                "Database connection failed", 
                ex,
                new Dictionary<string, object>
                {
                    ["duration_ms"] = stopwatch.ElapsedMilliseconds,
                    ["error"] = ex.Message
                });
        }
        finally
        {
            if (_connection.State == System.Data.ConnectionState.Open)
            {
                await _connection.CloseAsync();
            }
        }
    }
}

public class HttpHealthCheck : IHealthCheckProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly ILogger<HttpHealthCheck> _logger;

    public string Name { get; }

    public HttpHealthCheck(HttpClient httpClient, string endpoint, string name, ILogger<HttpHealthCheck> logger)
    {
        _httpClient = httpClient;
        _endpoint = endpoint;
        Name = name;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var response = await _httpClient.GetAsync(_endpoint, cancellationToken);
            stopwatch.Stop();
            
            var data = new Dictionary<string, object>
            {
                ["duration_ms"] = stopwatch.ElapsedMilliseconds,
                ["status_code"] = (int)response.StatusCode,
                ["endpoint"] = _endpoint
            };

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("HTTP health check for {Name} succeeded in {Duration}ms", Name, stopwatch.ElapsedMilliseconds);
                return HealthCheckResult.Healthy($"HTTP endpoint {_endpoint} is healthy", data);
            }
            else
            {
                _logger.LogWarning("HTTP health check for {Name} returned {StatusCode} in {Duration}ms", 
                    Name, response.StatusCode, stopwatch.ElapsedMilliseconds);
                return HealthCheckResult.Degraded($"HTTP endpoint {_endpoint} returned {response.StatusCode}", data: data);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "HTTP health check for {Name} failed after {Duration}ms", Name, stopwatch.ElapsedMilliseconds);
            
            return HealthCheckResult.Unhealthy(
                $"HTTP endpoint {_endpoint} is unreachable",
                ex,
                new Dictionary<string, object>
                {
                    ["duration_ms"] = stopwatch.ElapsedMilliseconds,
                    ["endpoint"] = _endpoint,
                    ["error"] = ex.Message
                });
        }
    }
}

public class MemoryHealthCheck : IHealthCheckProvider
{
    private readonly long _thresholdBytes;
    private readonly ILogger<MemoryHealthCheck> _logger;

    public string Name => "Memory";

    public MemoryHealthCheck(long thresholdBytes = 1024 * 1024 * 1024, ILogger<MemoryHealthCheck>? logger = null) // 1GB default
    {
        _thresholdBytes = thresholdBytes;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryHealthCheck>.Instance;
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var allocatedBytes = GC.GetTotalMemory(false);
        var data = new Dictionary<string, object>
        {
            ["allocated_bytes"] = allocatedBytes,
            ["threshold_bytes"] = _thresholdBytes,
            ["allocated_mb"] = allocatedBytes / (1024 * 1024),
            ["threshold_mb"] = _thresholdBytes / (1024 * 1024)
        };

        if (allocatedBytes >= _thresholdBytes)
        {
            _logger.LogWarning("Memory usage {AllocatedMB}MB exceeds threshold {ThresholdMB}MB", 
                allocatedBytes / (1024 * 1024), _thresholdBytes / (1024 * 1024));
            
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage {allocatedBytes / (1024 * 1024)}MB exceeds threshold {_thresholdBytes / (1024 * 1024)}MB",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage {allocatedBytes / (1024 * 1024)}MB is below threshold {_thresholdBytes / (1024 * 1024)}MB",
            data));
    }
} 