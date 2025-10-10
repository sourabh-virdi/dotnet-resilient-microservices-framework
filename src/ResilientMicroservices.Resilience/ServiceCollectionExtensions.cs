using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResilientMicroservices.Core;

namespace ResilientMicroservices.Resilience;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientMicroservicesResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        // Configure options
        services.Configure<CircuitBreakerOptions>(options =>
            configuration.GetSection("ResilientMicroservices:CircuitBreaker").Bind(options));
        services.Configure<RetryPolicyOptions>(options =>
            configuration.GetSection("ResilientMicroservices:RetryPolicy").Bind(options));
        services.Configure<TimeoutOptions>(options =>
            configuration.GetSection("ResilientMicroservices:Timeout").Bind(options));

        // Register services
        services.TryAddSingleton<ICircuitBreaker, PollyCircuitBreaker>();
        services.TryAddSingleton<IRetryPolicyService, RetryPolicyService>();
        services.TryAddSingleton<ITimeoutService, TimeoutService>();
        services.AddHttpClient<IResilientHttpClient, ResilientHttpClient>();

        return services;
    }

    public static IServiceCollection AddResilientMicroservicesResilience(
        this IServiceCollection services,
        Action<ResilienceOptionsBuilder> configure)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        var builder = new ResilienceOptionsBuilder();
        configure(builder);

        // Configure options from builder
        services.Configure<CircuitBreakerOptions>(options =>
        {
            options.FailureThreshold = builder.CircuitBreakerOptions.FailureThreshold;
            options.OpenTimeout = builder.CircuitBreakerOptions.OpenTimeout;
            options.SamplingDuration = builder.CircuitBreakerOptions.SamplingDuration;
            options.MinimumThroughput = builder.CircuitBreakerOptions.MinimumThroughput;
        });

        services.Configure<RetryPolicyOptions>(options =>
        {
            options.MaxRetryAttempts = builder.RetryPolicyOptions.MaxRetryAttempts;
            options.BaseDelay = builder.RetryPolicyOptions.BaseDelay;
            options.UseExponentialBackoff = builder.RetryPolicyOptions.UseExponentialBackoff;
            options.BackoffMultiplier = builder.RetryPolicyOptions.BackoffMultiplier;
            options.UseJitter = builder.RetryPolicyOptions.UseJitter;
            options.MaxJitter = builder.RetryPolicyOptions.MaxJitter;
        });

        services.Configure<TimeoutOptions>(options =>
        {
            options.DefaultTimeout = builder.TimeoutOptions.DefaultTimeout;
        });

        // Register services
        services.TryAddSingleton<ICircuitBreaker, PollyCircuitBreaker>();
        services.TryAddSingleton<IRetryPolicyService, RetryPolicyService>();
        services.TryAddSingleton<ITimeoutService, TimeoutService>();
        services.AddHttpClient<IResilientHttpClient, ResilientHttpClient>();

        return services;
    }
}

public class ResilienceOptionsBuilder
{
    public CircuitBreakerOptions CircuitBreakerOptions { get; } = new();
    public RetryPolicyOptions RetryPolicyOptions { get; } = new();
    public TimeoutOptions TimeoutOptions { get; } = new();

    public ResilienceOptionsBuilder WithCircuitBreaker(Action<CircuitBreakerOptions> configure)
    {
        configure(CircuitBreakerOptions);
        return this;
    }

    public ResilienceOptionsBuilder WithRetryPolicy(Action<RetryPolicyOptions> configure)
    {
        configure(RetryPolicyOptions);
        return this;
    }

    public ResilienceOptionsBuilder WithTimeout(Action<TimeoutOptions> configure)
    {
        configure(TimeoutOptions);
        return this;
    }
} 