using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Resilience;

public interface ITimeoutService
{
    Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    Task ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}

public class TimeoutService : ITimeoutService
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<TimeoutService> _logger;
    private readonly TimeoutOptions _options;

    public TimeoutService(
        IOptions<TimeoutOptions> options,
        ILogger<TimeoutService> logger)
    {
        _logger = logger;
        _options = options.Value;

        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = _options.DefaultTimeout,
                OnTimeout = args =>
                {
                    _logger.LogWarning("Operation timed out after {Timeout}", args.Timeout);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (timeout.HasValue && timeout != _options.DefaultTimeout)
        {
            // Create a new pipeline with custom timeout
            var customPipeline = new ResiliencePipelineBuilder()
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = timeout.Value,
                    OnTimeout = args =>
                    {
                        _logger.LogWarning("Operation timed out after {Timeout}", args.Timeout);
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            return await customPipeline.ExecuteAsync(async token => await operation(token), cancellationToken);
        }

        return await _pipeline.ExecuteAsync(async token => await operation(token), cancellationToken);
    }

    public async Task ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (timeout.HasValue && timeout != _options.DefaultTimeout)
        {
            // Create a new pipeline with custom timeout
            var customPipeline = new ResiliencePipelineBuilder()
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = timeout.Value,
                    OnTimeout = args =>
                    {
                        _logger.LogWarning("Operation timed out after {Timeout}", args.Timeout);
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            await customPipeline.ExecuteAsync(async token =>
            {
                await operation(token);
                return ValueTask.CompletedTask;
            }, cancellationToken);
            return;
        }

        await _pipeline.ExecuteAsync(async token =>
        {
            await operation(token);
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }
}

public class TimeoutOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
} 