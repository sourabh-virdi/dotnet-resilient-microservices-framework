using PaymentService.Services;
using ResilientMicroservices.Core;
using ResilientMicroservices.Core.HealthChecks;
using ResilientMicroservices.Messaging;
using ResilientMicroservices.Metrics;
using ResilientMicroservices.Resilience;
using ResilientMicroservices.Tracing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Payment Service", Version = "v1" });
});

// Register framework components
builder.Services.AddResilientMicroservicesCore();
builder.Services.AddResilientMicroservicesResilience(builder.Configuration);
builder.Services.AddResilientMicroservicesTracing(options =>
{
    options.WithService("PaymentService")
           .WithConsoleExporter(false)
           .WithJaegerExporter();
});
builder.Services.AddResilientMicroservicesMessaging(options =>
{
    var rabbitMqSection = builder.Configuration.GetSection("ResilientMicroservices:RabbitMQ");
    rabbitMqSection.Bind(options);
});
builder.Services.AddResilientMicroservicesMetrics(options =>
{
    options.WithPrometheusExporter()
           .WithConsoleExporter(false);
});

// Register application services
builder.Services.AddScoped<IPaymentService, PaymentServiceImpl>();

// Add health checks
builder.Services.AddHealthChecks(options =>
{
    options.AddProvider<MemoryHealthCheck>();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

// Add Prometheus metrics endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseAuthorization();
app.MapControllers();

// Add health check endpoint
app.MapGet("/health", async (IEnumerable<IHealthCheckProvider> healthChecks) =>
{
    var results = new List<object>();
    
    foreach (var healthCheck in healthChecks)
    {
        var result = await healthCheck.CheckHealthAsync();
        results.Add(new
        {
            name = healthCheck.Name,
            status = result.Status.ToString(),
            description = result.Description,
            duration_ms = result.Data?.GetValueOrDefault("duration_ms", 0),
            timestamp = DateTime.UtcNow
        });
    }

    var overallStatus = results.Any(r => r.GetType().GetProperty("status")?.GetValue(r)?.ToString() != "Healthy") 
        ? "Unhealthy" : "Healthy";

    return Results.Ok(new
    {
        status = overallStatus,
        service = "PaymentService",
        checks = results,
        timestamp = DateTime.UtcNow
    });
});

Console.WriteLine("🚀 Payment Service starting...");
Console.WriteLine("📖 Swagger UI available at: http://localhost:5002");
Console.WriteLine("💳 Payment API available at: http://localhost:5002/api/payments");
Console.WriteLine("🏥 Health check available at: http://localhost:5002/health");
Console.WriteLine("📊 Metrics available at: http://localhost:5002/metrics");

app.Run();
