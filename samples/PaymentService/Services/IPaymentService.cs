using PaymentService.Models;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task<ProcessPaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request);
    Task<Payment?> GetPaymentAsync(int paymentId);
    Task<RefundPaymentResponse> RefundPaymentAsync(RefundPaymentRequest request);
    Task<IEnumerable<Payment>> GetPaymentsByOrderAsync(int orderId);
    Task<bool> IsHealthyAsync();
} 