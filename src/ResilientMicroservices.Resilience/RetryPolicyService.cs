using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Resilience;

public interface IRetryPolicyService
{
    Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
    Task ExecuteWithRetryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}

public class RetryPolicyService : IRetryPolicyService
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<RetryPolicyService> _logger;

    public RetryPolicyService(
        IOptions<RetryPolicyOptions> options,
        ILogger<RetryPolicyService> logger)
    {
        _logger = logger;
        var retryOptions = options.Value;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                DelayGenerator = args =>
                {
                    var delay = retryOptions.BaseDelay;
                    if (retryOptions.UseExponentialBackoff)
                    {
                        delay = TimeSpan.FromMilliseconds(
                            retryOptions.BaseDelay.TotalMilliseconds * Math.Pow(retryOptions.BackoffMultiplier, args.AttemptNumber));
                    }

                    if (retryOptions.UseJitter)
                    {
                        var jitter = Random.Shared.NextDouble() * retryOptions.MaxJitter.TotalMilliseconds;
                        delay = delay.Add(TimeSpan.FromMilliseconds(jitter));
                    }

                    return new ValueTask<TimeSpan?>(delay);
                },
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry attempt {AttemptNumber} for operation. Exception: {Exception}",
                        args.AttemptNumber,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(async token => await operation(token), cancellationToken);
    }

    public async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            await operation(token);
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }
}

public class RetryPolicyOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool UseExponentialBackoff { get; set; } = true;
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;
    public TimeSpan MaxJitter { get; set; } = TimeSpan.FromMilliseconds(100);
} 