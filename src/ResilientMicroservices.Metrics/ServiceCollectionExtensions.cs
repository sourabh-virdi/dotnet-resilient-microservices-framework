using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using ResilientMicroservices.Core;

namespace ResilientMicroservices.Metrics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientMicroservicesMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var metricsSection = configuration.GetSection("ResilientMicroservices:Metrics");
        return services.AddResilientMicroservicesMetrics(options =>
        {
            metricsSection.Bind(options);
        });
    }

    public static IServiceCollection AddResilientMicroservicesMetrics(
        this IServiceCollection services,
        Action<MetricsOptionsBuilder> configure)
    {
        var builder = new MetricsOptionsBuilder();
        configure(builder);

        // Register the metrics collector
        services.TryAddSingleton<IMetricsCollector, DiagnosticsMetricsCollector>();

        // Register metrics background service for system metrics
        services.AddHostedService<MetricsBackgroundService>();

        // Configure OpenTelemetry Metrics
        services.AddOpenTelemetry()
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .AddMeter("ResilientMicroservices")
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation();

                // Add Prometheus exporter if enabled
                if (builder.EnablePrometheusExporter)
                {
                    metricsBuilder.AddPrometheusExporter();
                }

                // Add console exporter if enabled
                if (builder.EnableConsoleExporter)
                {
                    metricsBuilder.AddConsoleExporter();
                }

                // Add custom exporters
                foreach (var exporterConfig in builder.CustomExporters)
                {
                    exporterConfig(metricsBuilder);
                }
            });

        return services;
    }
}

public class MetricsOptionsBuilder
{
    public bool EnablePrometheusExporter { get; set; } = true;
    public bool EnableConsoleExporter { get; set; } = false;
    public TimeSpan SystemMetricsInterval { get; set; } = TimeSpan.FromSeconds(30);
    public List<Action<MeterProviderBuilder>> CustomExporters { get; } = new();

    public MetricsOptionsBuilder WithPrometheusExporter(bool enabled = true)
    {
        EnablePrometheusExporter = enabled;
        return this;
    }

    public MetricsOptionsBuilder WithConsoleExporter(bool enabled = true)
    {
        EnableConsoleExporter = enabled;
        return this;
    }

    public MetricsOptionsBuilder WithSystemMetricsInterval(TimeSpan interval)
    {
        SystemMetricsInterval = interval;
        return this;
    }

    public MetricsOptionsBuilder WithCustomExporter(Action<MeterProviderBuilder> configureExporter)
    {
        CustomExporters.Add(configureExporter);
        return this;
    }
}

internal class MetricsBackgroundService : BackgroundService
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly TimeSpan _interval;

    public MetricsBackgroundService(IMetricsCollector metricsCollector)
    {
        _metricsCollector = metricsCollector;
        _interval = TimeSpan.FromSeconds(30); // Default interval
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_metricsCollector is DiagnosticsMetricsCollector diagnosticsCollector)
                {
                    diagnosticsCollector.UpdateSystemMetrics();
                }
                
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception)
            {
                // Log and continue
                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
} 