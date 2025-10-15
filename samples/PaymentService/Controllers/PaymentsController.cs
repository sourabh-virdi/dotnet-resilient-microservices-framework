using Microsoft.AspNetCore.Mvc;
using PaymentService.Models;
using PaymentService.Services;
using ResilientMicroservices.Core;
using ResilientMicroservices.Core.HealthChecks;
using ResilientMicroservices.Resilience;
using System.Diagnostics;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IDistributedTracing _tracing;
    private readonly IMessageBus _messageBus;
    private readonly IMetricsCollector _metrics;
    private readonly IRetryPolicyService _retryPolicy;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IDistributedTracing tracing,
        IMessageBus messageBus,
        IMetricsCollector metrics,
        IRetryPolicyService retryPolicy,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _tracing = tracing;
        _messageBus = messageBus;
        _metrics = metrics;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ProcessPaymentResponse>> ProcessPayment(
        [FromBody] ProcessPaymentRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("ProcessPayment");
        _tracing.AddTag("order_id", request.OrderId.ToString());
        _tracing.AddTag("amount", request.Amount.ToString());
        _tracing.AddTag("payment_method", request.PaymentMethod);

        try
        {
            _logger.LogInformation("Processing payment for order {OrderId}, amount {Amount}", 
                request.OrderId, request.Amount);

            var result = await _retryPolicy.ExecuteWithRetryAsync(
                async (ct) => await _paymentService.ProcessPaymentAsync(request),
                CancellationToken.None);

            stopwatch.Stop();

            // Record metrics
            _metrics.RecordHttpRequest("POST", "/api/payments", 200, stopwatch.Elapsed);

            // Publish payment processed event
            var paymentEvent = new PaymentProcessedEvent
            {
                PaymentId = result.PaymentId,
                OrderId = request.OrderId,
                Amount = request.Amount,
                Status = result.Status,
                TransactionId = result.TransactionId,
                FailureReason = result.Status == PaymentStatus.Failed ? result.Message : null
            };

            await _messageBus.PublishAsync(paymentEvent, "payment.processed");

            _tracing.AddEvent("PaymentProcessed", tags: new[]
            {
                new KeyValuePair<string, object?>("payment_id", result.PaymentId),
                new KeyValuePair<string, object?>("status", result.Status.ToString())
            });

            _logger.LogInformation("Payment processed successfully: {PaymentId}, Status: {Status}", 
                result.PaymentId, result.Status);

            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("POST", "/api/payments", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", request.OrderId);
            
            return StatusCode(500, new { message = "Payment processing failed", error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Payment>> GetPayment(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("GetPayment");
        _tracing.AddTag("payment_id", id.ToString());

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);
            
            stopwatch.Stop();
            _metrics.RecordHttpRequest("GET", "/api/payments/{id}", 200, stopwatch.Elapsed);

            if (payment == null)
            {
                _logger.LogWarning("Payment {PaymentId} not found", id);
                return NotFound(new { message = $"Payment {id} not found" });
            }

            return Ok(payment);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/payments/{id}", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to get payment {PaymentId}", id);
            
            return StatusCode(500, new { message = "Failed to retrieve payment", error = ex.Message });
        }
    }

    [HttpPost("{id}/refund")]
    public async Task<ActionResult<RefundPaymentResponse>> RefundPayment(
        int id, 
        [FromBody] RefundPaymentRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("RefundPayment");
        _tracing.AddTag("payment_id", id.ToString());
        _tracing.AddTag("refund_amount", request.Amount?.ToString() ?? "full");

        try
        {
            request.PaymentId = id;

            _logger.LogInformation("Processing refund for payment {PaymentId}, amount {Amount}", 
                id, request.Amount ?? 0);

            var result = await _retryPolicy.ExecuteWithRetryAsync(
                async (ct) => await _paymentService.RefundPaymentAsync(request),
                CancellationToken.None);

            stopwatch.Stop();
            _metrics.RecordHttpRequest("POST", "/api/payments/{id}/refund", 200, stopwatch.Elapsed);

            _tracing.AddEvent("PaymentRefunded", tags: new[]
            {
                new KeyValuePair<string, object?>("refund_id", result.RefundId),
                new KeyValuePair<string, object?>("amount", result.Amount)
            });

            _logger.LogInformation("Payment refunded successfully: {RefundId}, Amount: {Amount}", 
                result.RefundId, result.Amount);

            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("POST", "/api/payments/{id}/refund", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to refund payment {PaymentId}", id);
            
            return StatusCode(500, new { message = "Payment refund failed", error = ex.Message });
        }
    }

    [HttpGet("health")]
    public async Task<ActionResult> HealthCheck()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var isHealthy = await _paymentService.IsHealthyAsync();
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/payments/health", 200, stopwatch.Elapsed);

            return Ok(new
            {
                status = isHealthy ? "healthy" : "unhealthy",
                service = "PaymentService",
                timestamp = DateTime.UtcNow,
                duration_ms = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/payments/health", 500, stopwatch.Elapsed);
            _logger.LogError(ex, "Health check failed");
            
            return StatusCode(500, new { status = "unhealthy", error = ex.Message });
        }
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<IEnumerable<Payment>>> GetPaymentsByOrder(int orderId)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("GetPaymentsByOrder");
        _tracing.AddTag("order_id", orderId.ToString());

        try
        {
            var payments = await _paymentService.GetPaymentsByOrderAsync(orderId);
            
            stopwatch.Stop();
            _metrics.RecordHttpRequest("GET", "/api/payments/order/{orderId}", 200, stopwatch.Elapsed);

            return Ok(payments);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/payments/order/{orderId}", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to get payments for order {OrderId}", orderId);
            
            return StatusCode(500, new { message = "Failed to retrieve payments", error = ex.Message });
        }
    }
} 