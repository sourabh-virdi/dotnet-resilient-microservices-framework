using OrderService.Sagas;
using ResilientMicroservices.Resilience;
using ResilientMicroservices.Tracing;
using ResilientMicroservices.Messaging;
using ResilientMicroservices.Sagas;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// Add Resilient Microservices Framework
builder.Services.AddResilientMicroservicesResilience(builder.Configuration);
builder.Services.AddResilientMicroservicesTracing(builder.Configuration, "OrderService", "1.0.0");
builder.Services.AddResilientMicroservicesMessaging(builder.Configuration, "OrderService");
builder.Services.AddResilientMicroservicesSagas();

// Add saga components
builder.Services.AddTransient<CreatePaymentStep>();
builder.Services.AddTransient<ReserveInventoryStep>();
builder.Services.AddTransient<ConfirmOrderStep>();
builder.Services.AddTransient<OrderProcessingSaga>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
