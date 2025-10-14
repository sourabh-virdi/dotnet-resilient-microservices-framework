using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using OrderService.Sagas;
using ResilientMicroservices.Core;
using ResilientMicroservices.Resilience;
using ResilientMicroservices.Sagas;
using System.Diagnostics;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ISagaOrchestrator _sagaOrchestrator;
    private readonly OrderProcessingSaga _orderSaga;
    private readonly IDistributedTracing _tracing;
    private readonly IMessageBus _messageBus;
    private readonly IRetryPolicyService _retryService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        ISagaOrchestrator sagaOrchestrator,
        OrderProcessingSaga orderSaga,
        IDistributedTracing tracing,
        IMessageBus messageBus,
        IRetryPolicyService retryService,
        ILogger<OrdersController> logger)
    {
        _sagaOrchestrator = sagaOrchestrator;
        _orderSaga = orderSaga;
        _tracing = tracing;
        _messageBus = messageBus;
        _retryService = retryService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        using var activity = _tracing.StartActivity("CreateOrder", ActivityKind.Server);
        
        _tracing.AddTag("order.customerId", request.CustomerId.ToString());
        _tracing.AddTag("order.itemCount", request.Items.Count.ToString());

        try
        {
            // Generate order ID
            var orderId = Random.Shared.Next(1000, 9999);
            var totalAmount = request.Items.Sum(x => x.Price * x.Quantity);

            _logger.LogInformation("Creating order {OrderId} for customer {CustomerId} with amount {Amount}", 
                orderId, request.CustomerId, totalAmount);

            _tracing.AddTag("order.id", orderId.ToString());
            _tracing.AddTag("order.amount", totalAmount.ToString());

            // Create the order using saga pattern
            var sagaData = new OrderProcessingSagaData
            {
                OrderId = orderId,
                CustomerId = request.CustomerId,
                Amount = totalAmount
            };

            var sagaResult = await _sagaOrchestrator.ExecuteSagaAsync(_orderSaga, sagaData);

            if (sagaResult.IsSuccess)
            {
                // Publish order created event with retry policy
                await _retryService.ExecuteWithRetryAsync(async token =>
                {
                    await _messageBus.PublishAsync(new OrderCreatedEvent
                    {
                        OrderId = orderId,
                        CustomerId = request.CustomerId,
                        Amount = totalAmount,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken: token);
                });

                _tracing.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation("Order {OrderId} created successfully", orderId);

                return Ok(new
                {
                    OrderId = orderId,
                    Status = "Created",
                    Amount = totalAmount,
                    PaymentId = sagaData.PaymentId,
                    InventoryReservationId = sagaData.InventoryReservationId
                });
            }
            else
            {
                _tracing.SetStatus(ActivityStatusCode.Error, sagaResult.ErrorMessage);
                _logger.LogWarning("Failed to create order: {ErrorMessage}", sagaResult.ErrorMessage);
                
                return BadRequest(new
                {
                    Error = "Order creation failed",
                    Message = sagaResult.ErrorMessage
                });
            }
        }
        catch (Exception ex)
        {
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Unexpected error creating order");
            
            return StatusCode(500, new
            {
                Error = "Internal server error",
                Message = ex.Message
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        using var activity = _tracing.StartActivity("GetOrder", ActivityKind.Server);
        _tracing.AddTag("order.id", id.ToString());

        try
        {
            // Simulate order retrieval with circuit breaker pattern (using retry service as example)
            var order = await _retryService.ExecuteWithRetryAsync(async token =>
            {
                _logger.LogDebug("Retrieving order {OrderId}", id);
                
                // Simulate database call
                await Task.Delay(50, token);
                
                return new Order
                {
                    Id = id,
                    CustomerId = Random.Shared.Next(1, 100),
                    TotalAmount = Random.Shared.Next(100, 1000),
                    Status = OrderStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 24)),
                    Items = new List<OrderItem>
                    {
                        new() { ProductId = 1, Quantity = 2, Price = 25.00m },
                        new() { ProductId = 2, Quantity = 1, Price = 50.00m }
                    }
                };
            });

            _tracing.SetStatus(ActivityStatusCode.Ok);
            return Ok(order);
        }
        catch (Exception ex)
        {
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error retrieving order {OrderId}", id);
            
            return StatusCode(500, new
            {
                Error = "Failed to retrieve order",
                Message = ex.Message
            });
        }
    }

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "OrderService",
            Timestamp = DateTime.UtcNow,
            TraceId = _tracing.GetTraceId()
        });
    }

    [HttpPost("simulate-failure")]
    public async Task<IActionResult> SimulateFailure()
    {
        using var activity = _tracing.StartActivity("SimulateFailure", ActivityKind.Server);
        
        try
        {
            // This will demonstrate compensation flow
            var sagaData = new OrderProcessingSagaData
            {
                OrderId = 9999,
                CustomerId = 1,
                Amount = 999.99m
            };

            // Force a failure by throwing an exception after first step  
            var modifiedSaga = new FailingOrderProcessingSaga(_logger);
            
            var sagaResult = await _sagaOrchestrator.ExecuteSagaAsync(modifiedSaga, sagaData);

            _tracing.SetStatus(ActivityStatusCode.Error, "Simulated failure");
            
            return Ok(new
            {
                Message = "Failure simulation completed",
                SagaResult = sagaResult.IsSuccess ? "Success" : "Failed",
                ErrorMessage = sagaResult.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

// Saga that fails after the first step to demonstrate compensation
public class FailingOrderProcessingSaga : BaseSaga<OrderProcessingSagaData>
{
    public FailingOrderProcessingSaga(ILogger logger) : base(logger)
    {
        AddStep(new FailingCreatePaymentStep(logger));
        AddStep(new FailingReserveInventoryStep(logger));
    }
}

public class FailingCreatePaymentStep : ISagaStep<OrderProcessingSagaData>
{
    private readonly ILogger _logger;
    
    public string StepName => "FailingCreatePayment";
    public int Order => 1;

    public FailingCreatePaymentStep(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating payment for order {OrderId} (will succeed)", data.OrderId);
        await Task.Delay(100, cancellationToken);
        data.PaymentId = 7777;
        return StepResult.Success();
    }

    public async Task<StepResult> CompensateAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Compensating payment {PaymentId} for order {OrderId}", data.PaymentId, data.OrderId);
        await Task.Delay(50, cancellationToken);
        return StepResult.Success();
    }
}

public class FailingReserveInventoryStep : ISagaStep<OrderProcessingSagaData>
{
    private readonly ILogger _logger;
    
    public string StepName => "FailingReserveInventory";
    public int Order => 2;

    public FailingReserveInventoryStep(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to reserve inventory for order {OrderId} (will fail)", data.OrderId);
        await Task.Delay(100, cancellationToken);
        return StepResult.Failure("Simulated inventory reservation failure");
    }

    public async Task<StepResult> CompensateAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("No compensation needed for inventory reservation");
        return StepResult.Success();
    }
} 