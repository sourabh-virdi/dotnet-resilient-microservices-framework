using Microsoft.Extensions.Logging;
using OrderService.Models;
using ResilientMicroservices.Core;
using ResilientMicroservices.Sagas;

namespace OrderService.Sagas;

public class OrderProcessingSagaData
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public int? PaymentId { get; set; }
    public int? InventoryReservationId { get; set; }
}

public class OrderProcessingSaga : BaseSaga<OrderProcessingSagaData>
{
    public OrderProcessingSaga(
        ILogger<OrderProcessingSaga> logger,
        CreatePaymentStep createPaymentStep,
        ReserveInventoryStep reserveInventoryStep,
        ConfirmOrderStep confirmOrderStep)
        : base(logger)
    {
        AddStep(createPaymentStep);
        AddStep(reserveInventoryStep);
        AddStep(confirmOrderStep);
    }
}

public class CreatePaymentStep : ISagaStep<OrderProcessingSagaData>
{
    private readonly ILogger<CreatePaymentStep> _logger;

    public string StepName => "CreatePayment";
    public int Order => 1;

    public CreatePaymentStep(ILogger<CreatePaymentStep> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating payment for order {OrderId}, amount {Amount}", data.OrderId, data.Amount);
            
            // Simulate payment creation
            await Task.Delay(100, cancellationToken);
            data.PaymentId = Random.Shared.Next(1000, 9999);
            
            _logger.LogInformation("Payment {PaymentId} created for order {OrderId}", data.PaymentId, data.OrderId);
            return StepResult.Success();
        }
        catch (Exception ex)
        {
            return StepResult.Failure($"Payment creation failed: {ex.Message}", ex);
        }
    }

    public async Task<StepResult> CompensateAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        if (data.PaymentId.HasValue)
        {
            _logger.LogInformation("Cancelling payment {PaymentId} for order {OrderId}", data.PaymentId, data.OrderId);
            await Task.Delay(50, cancellationToken);
        }
        return StepResult.Success();
    }
}

public class ReserveInventoryStep : ISagaStep<OrderProcessingSagaData>
{
    private readonly ILogger<ReserveInventoryStep> _logger;

    public string StepName => "ReserveInventory";
    public int Order => 2;

    public ReserveInventoryStep(ILogger<ReserveInventoryStep> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Reserving inventory for order {OrderId}", data.OrderId);
            
            // Simulate inventory reservation
            await Task.Delay(150, cancellationToken);
            data.InventoryReservationId = Random.Shared.Next(1000, 9999);
            
            _logger.LogInformation("Inventory reservation {ReservationId} created for order {OrderId}", 
                data.InventoryReservationId, data.OrderId);
            return StepResult.Success();
        }
        catch (Exception ex)
        {
            return StepResult.Failure($"Inventory reservation failed: {ex.Message}", ex);
        }
    }

    public async Task<StepResult> CompensateAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        if (data.InventoryReservationId.HasValue)
        {
            _logger.LogInformation("Releasing inventory reservation {ReservationId} for order {OrderId}", 
                data.InventoryReservationId, data.OrderId);
            await Task.Delay(50, cancellationToken);
        }
        return StepResult.Success();
    }
}

public class ConfirmOrderStep : ISagaStep<OrderProcessingSagaData>
{
    private readonly ILogger<ConfirmOrderStep> _logger;

    public string StepName => "ConfirmOrder";
    public int Order => 3;

    public ConfirmOrderStep(ILogger<ConfirmOrderStep> logger)
    {
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Confirming order {OrderId}", data.OrderId);
            
            // Simulate order confirmation
            await Task.Delay(100, cancellationToken);
            
            _logger.LogInformation("Order {OrderId} confirmed successfully", data.OrderId);
            return StepResult.Success();
        }
        catch (Exception ex)
        {
            return StepResult.Failure($"Order confirmation failed: {ex.Message}", ex);
        }
    }

    public async Task<StepResult> CompensateAsync(OrderProcessingSagaData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling order {OrderId}", data.OrderId);
        await Task.Delay(50, cancellationToken);
        return StepResult.Success();
    }
} 