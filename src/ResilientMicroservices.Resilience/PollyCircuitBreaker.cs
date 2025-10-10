using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using ResilientMicroservices.Core;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Resilience;

public class PollyCircuitBreaker : ICircuitBreaker
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<PollyCircuitBreaker> _logger;
    private readonly CircuitBreakerOptions _options;

    public PollyCircuitBreaker(
        IOptions<CircuitBreakerOptions> options,
        ILogger<PollyCircuitBreaker> logger)
    {
        _options = options.Value;
        _logger = logger;
        Name = $"CircuitBreaker-{Guid.NewGuid():N}";

        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = (double)_options.FailureThreshold / _options.SamplingDuration,
                SamplingDuration = TimeSpan.FromSeconds(_options.SamplingDuration),
                MinimumThroughput = _options.MinimumThroughput,
                BreakDuration = _options.OpenTimeout,
                OnOpened = args =>
                {
                    _logger.LogWarning("Circuit breaker {Name} opened", Name);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker {Name} closed", Name);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker {Name} half-opened", Name);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public string Name { get; }

    public CircuitBreakerState State
    {
        get
        {
            // This is a simplified approach - in a real implementation you might need to track state
            return CircuitBreakerState.Closed; // Default state
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _pipeline.ExecuteAsync(async token => await operation(token), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Circuit breaker {Name} operation failed", Name);
            throw;
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _pipeline.ExecuteAsync(async token =>
            {
                await operation(token);
                return ValueTask.CompletedTask;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Circuit breaker {Name} operation failed", Name);
            throw;
        }
    }
} 