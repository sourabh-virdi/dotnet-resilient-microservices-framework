using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ResilientMicroservices.Core;
using System.Diagnostics;

namespace ResilientMicroservices.Tracing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientMicroservicesTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string? serviceVersion = null,
        string? environment = null)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        // Configure tracing options
        services.Configure<TracingOptions>(options =>
        {
            configuration.GetSection("ResilientMicroservices:Tracing").Bind(options);
            options.ServiceName = serviceName;
            options.ServiceVersion = serviceVersion ?? "1.0.0";
            options.Environment = environment ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        });

        // Replace the default distributed tracing with OpenTelemetry implementation
        services.Replace(ServiceDescriptor.Singleton<IDistributedTracing, OpenTelemetryDistributedTracing>());

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                var tracingOptions = new TracingOptions();
                configuration.GetSection("ResilientMicroservices:Tracing").Bind(tracingOptions);
                tracingOptions.ServiceName = serviceName;
                tracingOptions.ServiceVersion = serviceVersion ?? "1.0.0";
                tracingOptions.Environment = environment ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(tracingOptions.ServiceName, tracingOptions.ServiceVersion)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["environment"] = tracingOptions.Environment ?? "unknown",
                            ["host.name"] = Environment.MachineName
                        }))
                    .AddSource(tracingOptions.ServiceName)
                    .SetSampler(new TraceIdRatioBasedSampler(tracingOptions.SamplingRatio))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnableGrpcAspNetCoreSupport = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });

                if (tracingOptions.EnableConsoleExporter)
                {
                    builder.AddConsoleExporter();
                }

                if (tracingOptions.EnableJaegerExporter)
                {
                    builder.AddJaegerExporter(jaegerOptions =>
                    {
                        jaegerOptions.Endpoint = new Uri(tracingOptions.JaegerEndpoint);
                    });
                }
            });

        return services;
    }

    public static IServiceCollection AddResilientMicroservicesTracing(
        this IServiceCollection services,
        Action<TracingOptionsBuilder> configure)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        var builder = new TracingOptionsBuilder();
        configure(builder);

        // Configure tracing options
        services.Configure<TracingOptions>(options =>
        {
            options.ServiceName = builder.ServiceName;
            options.ServiceVersion = builder.ServiceVersion;
            options.Environment = builder.Environment;
            options.EnableConsoleExporter = builder.EnableConsoleExporter;
            options.EnableJaegerExporter = builder.EnableJaegerExporter;
            options.JaegerEndpoint = builder.JaegerEndpoint;
            options.SamplingRatio = builder.SamplingRatio;
        });

        // Replace the default distributed tracing with OpenTelemetry implementation
        services.Replace(ServiceDescriptor.Singleton<IDistributedTracing, OpenTelemetryDistributedTracing>());

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(builder.ServiceName, builder.ServiceVersion)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["environment"] = builder.Environment ?? "unknown",
                            ["host.name"] = Environment.MachineName
                        }))
                    .AddSource(builder.ServiceName)
                    .SetSampler(new TraceIdRatioBasedSampler(builder.SamplingRatio))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnableGrpcAspNetCoreSupport = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });

                if (builder.EnableConsoleExporter)
                {
                    tracingBuilder.AddConsoleExporter();
                }

                if (builder.EnableJaegerExporter)
                {
                    tracingBuilder.AddJaegerExporter(jaegerOptions =>
                    {
                        jaegerOptions.Endpoint = new Uri(builder.JaegerEndpoint);
                    });
                }
            });

        return services;
    }
}

public class TracingOptionsBuilder
{
    public string ServiceName { get; private set; } = "Unknown";
    public string ServiceVersion { get; private set; } = "1.0.0";
    public string? Environment { get; private set; }
    public bool EnableConsoleExporter { get; private set; } = true;
    public bool EnableJaegerExporter { get; private set; } = true;
    public string JaegerEndpoint { get; private set; } = "http://localhost:14268/api/traces";
    public double SamplingRatio { get; private set; } = 1.0;

    public TracingOptionsBuilder WithService(string serviceName, string? serviceVersion = null)
    {
        ServiceName = serviceName;
        ServiceVersion = serviceVersion ?? "1.0.0";
        return this;
    }

    public TracingOptionsBuilder WithEnvironment(string environment)
    {
        Environment = environment;
        return this;
    }

    public TracingOptionsBuilder WithConsoleExporter(bool enabled = true)
    {
        EnableConsoleExporter = enabled;
        return this;
    }

    public TracingOptionsBuilder WithJaegerExporter(bool enabled = true, string? endpoint = null)
    {
        EnableJaegerExporter = enabled;
        if (!string.IsNullOrEmpty(endpoint))
        {
            JaegerEndpoint = endpoint;
        }
        return this;
    }

    public TracingOptionsBuilder WithSampling(double ratio)
    {
        SamplingRatio = Math.Clamp(ratio, 0.0, 1.0);
        return this;
    }
} 