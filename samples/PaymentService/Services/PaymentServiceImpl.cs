using PaymentService.Models;
using ResilientMicroservices.Core;

namespace PaymentService.Services;

public class PaymentServiceImpl : IPaymentService
{
    private readonly ILogger<PaymentServiceImpl> _logger;
    private readonly IMetricsCollector _metrics;
    private readonly List<Payment> _payments = new();
    private readonly Random _random = new();
    private int _nextPaymentId = 1;
    private int _nextRefundId = 1;

    public PaymentServiceImpl(ILogger<PaymentServiceImpl> logger, IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<ProcessPaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        await Task.Delay(100); // Simulate processing time

        var payment = new Payment
        {
            Id = _nextPaymentId++,
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = PaymentStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };

        _payments.Add(payment);

        _logger.LogInformation("Processing payment {PaymentId} for order {OrderId}", 
            payment.Id, request.OrderId);

        // Simulate payment processing
        await Task.Delay(500); // Simulate external payment gateway delay

        // Simulate success/failure (90% success rate)
        var isSuccessful = _random.NextDouble() > 0.1;

        if (isSuccessful)
        {
            payment.Status = PaymentStatus.Completed;
            payment.TransactionId = Guid.NewGuid().ToString("N")[..8];
            payment.ProcessedAt = DateTime.UtcNow;

            _logger.LogInformation("Payment {PaymentId} completed successfully with transaction {TransactionId}", 
                payment.Id, payment.TransactionId);
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Insufficient funds or card declined";
            payment.ProcessedAt = DateTime.UtcNow;

            _logger.LogWarning("Payment {PaymentId} failed: {Reason}", 
                payment.Id, payment.FailureReason);
        }

        // Record metrics
        _metrics.RecordValue("payment_amount", (double)request.Amount);
        _metrics.IncrementCounter("payments_processed", 1, 
            new KeyValuePair<string, object?>("status", payment.Status.ToString()),
            new KeyValuePair<string, object?>("payment_method", request.PaymentMethod));

        return new ProcessPaymentResponse
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            TransactionId = payment.TransactionId,
            Message = payment.Status == PaymentStatus.Completed 
                ? "Payment processed successfully" 
                : payment.FailureReason
        };
    }

    public Task<Payment?> GetPaymentAsync(int paymentId)
    {
        var payment = _payments.FirstOrDefault(p => p.Id == paymentId);
        return Task.FromResult(payment);
    }

    public async Task<RefundPaymentResponse> RefundPaymentAsync(RefundPaymentRequest request)
    {
        var payment = _payments.FirstOrDefault(p => p.Id == request.PaymentId);
        
        if (payment == null)
        {
            throw new ArgumentException($"Payment {request.PaymentId} not found");
        }

        if (payment.Status != PaymentStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot refund payment {request.PaymentId} with status {payment.Status}");
        }

        var refundAmount = request.Amount ?? payment.Amount;

        if (refundAmount > payment.Amount)
        {
            throw new ArgumentException("Refund amount cannot exceed payment amount");
        }

        await Task.Delay(200); // Simulate refund processing

        // Simulate refund success/failure (95% success rate)
        var isSuccessful = _random.NextDouble() > 0.05;

        if (isSuccessful)
        {
            if (refundAmount == payment.Amount)
            {
                payment.Status = PaymentStatus.Refunded;
            }

            _logger.LogInformation("Refund processed for payment {PaymentId}, amount {Amount}", 
                request.PaymentId, refundAmount);

            // Record metrics
            _metrics.RecordValue("refund_amount", (double)refundAmount);
            _metrics.IncrementCounter("refunds_processed", 1, 
                new KeyValuePair<string, object?>("status", "completed"));

            return new RefundPaymentResponse
            {
                RefundId = _nextRefundId++,
                Amount = refundAmount,
                Status = PaymentStatus.Refunded,
                Message = "Refund processed successfully"
            };
        }
        else
        {
            _logger.LogError("Refund failed for payment {PaymentId}", request.PaymentId);

            _metrics.IncrementCounter("refunds_processed", 1, 
                new KeyValuePair<string, object?>("status", "failed"));

            throw new InvalidOperationException("Refund processing failed - external gateway error");
        }
    }

    public Task<IEnumerable<Payment>> GetPaymentsByOrderAsync(int orderId)
    {
        var payments = _payments.Where(p => p.OrderId == orderId).ToList();
        return Task.FromResult<IEnumerable<Payment>>(payments);
    }

    public Task<bool> IsHealthyAsync()
    {
        // Simple health check - in real implementation, this would check:
        // - Database connectivity
        // - External payment gateway availability
        // - System resources
        
        var isHealthy = _payments.Count < 10000; // Arbitrary health condition
        
        _metrics.IncrementCounter("health_checks", 1, 
            new KeyValuePair<string, object?>("service", "payment"),
            new KeyValuePair<string, object?>("status", isHealthy ? "healthy" : "unhealthy"));

        return Task.FromResult(isHealthy);
    }
} 