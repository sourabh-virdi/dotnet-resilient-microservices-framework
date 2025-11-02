using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentService.Models;
using PaymentService.Services;
using ResilientMicroservices.Core;

namespace PaymentService.Tests;

public class PaymentServiceImplTests
{
    private readonly Mock<ILogger<PaymentServiceImpl>> _mockLogger;
    private readonly Mock<IMetricsCollector> _mockMetrics;
    private readonly PaymentServiceImpl _paymentService;

    public PaymentServiceImplTests()
    {
        _mockLogger = new Mock<ILogger<PaymentServiceImpl>>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _paymentService = new PaymentServiceImpl(_mockLogger.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ValidRequest_ShouldReturnSuccessResponse()
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 99.99m,
            PaymentMethod = "credit_card",
            CustomerId = "customer123"
        };

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().BeGreaterThan(0);
        result.Status.Should().BeOneOf(PaymentStatus.Completed, PaymentStatus.Failed);
        
        if (result.Status == PaymentStatus.Completed)
        {
            result.TransactionId.Should().NotBeNullOrEmpty();
            result.Message.Should().Be("Payment processed successfully");
        }
        else
        {
            result.Message.Should().Contain("Insufficient funds or card declined");
        }

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.RecordValue("payment_amount", (double)request.Amount), Times.Once);
        _mockMetrics.Verify(m => m.IncrementCounter("payments_processed", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_MultipleRequests_ShouldGenerateUniquePaymentIds()
    {
        // Arrange
        var request1 = new ProcessPaymentRequest { OrderId = 1, Amount = 50m, PaymentMethod = "credit_card" };
        var request2 = new ProcessPaymentRequest { OrderId = 2, Amount = 75m, PaymentMethod = "debit_card" };

        // Act
        var result1 = await _paymentService.ProcessPaymentAsync(request1);
        var result2 = await _paymentService.ProcessPaymentAsync(request2);

        // Assert
        result1.PaymentId.Should().NotBe(result2.PaymentId);
        result1.PaymentId.Should().BeLessThan(result2.PaymentId);
    }

    [Fact]
    public async Task GetPaymentAsync_ExistingPayment_ShouldReturnPayment()
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 99.99m,
            PaymentMethod = "credit_card"
        };
        var processResult = await _paymentService.ProcessPaymentAsync(request);

        // Act
        var payment = await _paymentService.GetPaymentAsync(processResult.PaymentId);

        // Assert
        payment.Should().NotBeNull();
        payment!.Id.Should().Be(processResult.PaymentId);
        payment.OrderId.Should().Be(request.OrderId);
        payment.Amount.Should().Be(request.Amount);
        payment.PaymentMethod.Should().Be(request.PaymentMethod);
        payment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetPaymentAsync_NonExistentPayment_ShouldReturnNull()
    {
        // Act
        var payment = await _paymentService.GetPaymentAsync(99999);

        // Assert
        payment.Should().BeNull();
    }

    [Fact]
    public async Task RefundPaymentAsync_ValidCompletedPayment_ShouldReturnSuccessResponse()
    {
        // Arrange
        var processRequest = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 100m,
            PaymentMethod = "credit_card"
        };
        
        ProcessPaymentResponse processResult;
        do
        {
            processResult = await _paymentService.ProcessPaymentAsync(processRequest);
        } while (processResult.Status != PaymentStatus.Completed);

        var refundRequest = new RefundPaymentRequest
        {
            PaymentId = processResult.PaymentId,
            Amount = 50m,
            Reason = "Customer request"
        };

        // Act
        var result = await _paymentService.RefundPaymentAsync(refundRequest);

        // Assert
        result.Should().NotBeNull();
        result.RefundId.Should().BeGreaterThan(0);
        result.Amount.Should().Be(50m);
        result.Status.Should().Be(PaymentStatus.Refunded);
        result.Message.Should().Be("Refund processed successfully");

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.RecordValue("refund_amount", 50.0), Times.Once);
        _mockMetrics.Verify(m => m.IncrementCounter("refunds_processed", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
    }

    [Fact]
    public async Task RefundPaymentAsync_FullRefund_ShouldUpdatePaymentStatus()
    {
        // Arrange
        var processRequest = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 100m,
            PaymentMethod = "credit_card"
        };
        
        ProcessPaymentResponse processResult;
        do
        {
            processResult = await _paymentService.ProcessPaymentAsync(processRequest);
        } while (processResult.Status != PaymentStatus.Completed);

        var refundRequest = new RefundPaymentRequest
        {
            PaymentId = processResult.PaymentId,
            Amount = null, // Full refund
            Reason = "Order cancelled"
        };

        // Act
        var result = await _paymentService.RefundPaymentAsync(refundRequest);

        // Assert
        result.Amount.Should().Be(100m);
        result.Status.Should().Be(PaymentStatus.Refunded);

        // Verify payment status was updated
        var payment = await _paymentService.GetPaymentAsync(processResult.PaymentId);
        payment!.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task RefundPaymentAsync_NonExistentPayment_ShouldThrowException()
    {
        // Arrange
        var refundRequest = new RefundPaymentRequest
        {
            PaymentId = 99999,
            Amount = 50m
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _paymentService.RefundPaymentAsync(refundRequest));
        
        exception.Message.Should().Contain("Payment 99999 not found");
    }

    [Fact]
    public async Task RefundPaymentAsync_RefundAmountExceedsPayment_ShouldThrowException()
    {
        // Arrange
        var processRequest = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 50m,
            PaymentMethod = "credit_card"
        };
        
        ProcessPaymentResponse processResult;
        do
        {
            processResult = await _paymentService.ProcessPaymentAsync(processRequest);
        } while (processResult.Status != PaymentStatus.Completed);

        var refundRequest = new RefundPaymentRequest
        {
            PaymentId = processResult.PaymentId,
            Amount = 100m // More than payment amount
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _paymentService.RefundPaymentAsync(refundRequest));
        
        exception.Message.Should().Contain("Refund amount cannot exceed payment amount");
    }

    [Fact]
    public async Task GetPaymentsByOrderAsync_ExistingOrder_ShouldReturnPayments()
    {
        // Arrange
        var orderId = 123;
        var request1 = new ProcessPaymentRequest { OrderId = orderId, Amount = 50m, PaymentMethod = "credit_card" };
        var request2 = new ProcessPaymentRequest { OrderId = orderId, Amount = 25m, PaymentMethod = "debit_card" };
        var request3 = new ProcessPaymentRequest { OrderId = 456, Amount = 75m, PaymentMethod = "credit_card" };

        await _paymentService.ProcessPaymentAsync(request1);
        await _paymentService.ProcessPaymentAsync(request2);
        await _paymentService.ProcessPaymentAsync(request3);

        // Act
        var payments = await _paymentService.GetPaymentsByOrderAsync(orderId);

        // Assert
        payments.Should().HaveCount(2);
        payments.Should().OnlyContain(p => p.OrderId == orderId);
        payments.Should().Contain(p => p.Amount == 50m);
        payments.Should().Contain(p => p.Amount == 25m);
    }

    [Fact]
    public async Task GetPaymentsByOrderAsync_NonExistentOrder_ShouldReturnEmptyList()
    {
        // Act
        var payments = await _paymentService.GetPaymentsByOrderAsync(99999);

        // Assert
        payments.Should().BeEmpty();
    }

    [Fact]
    public async Task IsHealthyAsync_ServiceOperational_ShouldReturnTrue()
    {
        // Act
        var isHealthy = await _paymentService.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.IncrementCounter("health_checks", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
    }

    [Theory]
    [InlineData("credit_card")]
    [InlineData("debit_card")]
    [InlineData("paypal")]
    [InlineData("bank_transfer")]
    public async Task ProcessPaymentAsync_DifferentPaymentMethods_ShouldHandleAll(string paymentMethod)
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 50m,
            PaymentMethod = paymentMethod
        };

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().BeGreaterThan(0);
        result.Status.Should().BeOneOf(PaymentStatus.Completed, PaymentStatus.Failed);

        var payment = await _paymentService.GetPaymentAsync(result.PaymentId);
        payment!.PaymentMethod.Should().Be(paymentMethod);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ZeroAmount_ShouldStillProcess()
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            OrderId = 123,
            Amount = 0m,
            PaymentMethod = "credit_card"
        };

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.PaymentId.Should().BeGreaterThan(0);

        var payment = await _paymentService.GetPaymentAsync(result.PaymentId);
        payment!.Amount.Should().Be(0m);
    }
} 