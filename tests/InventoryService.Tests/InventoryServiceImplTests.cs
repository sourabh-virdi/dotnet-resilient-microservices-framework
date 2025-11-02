using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using InventoryService.Models;
using InventoryService.Services;
using ResilientMicroservices.Core;

namespace InventoryService.Tests;

public class InventoryServiceImplTests
{
    private readonly Mock<ILogger<InventoryServiceImpl>> _mockLogger;
    private readonly Mock<IMetricsCollector> _mockMetrics;
    private readonly InventoryServiceImpl _inventoryService;

    public InventoryServiceImplTests()
    {
        _mockLogger = new Mock<ILogger<InventoryServiceImpl>>();
        _mockMetrics = new Mock<IMetricsCollector>();
        _inventoryService = new InventoryServiceImpl(_mockLogger.Object, _mockMetrics.Object);
    }

    [Fact]
    public async Task GetAllInventoryAsync_ShouldReturnInitializedInventory()
    {
        // Act
        var inventory = await _inventoryService.GetAllInventoryAsync();

        // Assert
        inventory.Should().NotBeEmpty();
        inventory.Should().HaveCount(5); // Based on sample data initialization
        inventory.Should().Contain(item => item.ProductId == "PROD001" && item.ProductName == "Gaming Laptop");
        inventory.Should().Contain(item => item.ProductId == "PROD002" && item.ProductName == "Mechanical Keyboard");
        inventory.Should().AllSatisfy(item =>
        {
            item.AvailableQuantity.Should().BeGreaterThan(0);
            item.Price.Should().BeGreaterThan(0);
            item.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        });
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_ExistingProduct_ShouldReturnProduct()
    {
        // Act
        var item = await _inventoryService.GetInventoryByProductIdAsync("PROD001");

        // Assert
        item.Should().NotBeNull();
        item!.ProductId.Should().Be("PROD001");
        item.ProductName.Should().Be("Gaming Laptop");
        item.AvailableQuantity.Should().Be(50);
        item.Price.Should().Be(1299.99m);
        item.Category.Should().Be("Electronics");
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_NonExistentProduct_ShouldReturnNull()
    {
        // Act
        var item = await _inventoryService.GetInventoryByProductIdAsync("NONEXISTENT");

        // Assert
        item.Should().BeNull();
    }

    [Fact]
    public async Task CheckInventoryAsync_SufficientStock_ShouldReturnAvailable()
    {
        // Arrange
        var request = new CheckInventoryRequest
        {
            ProductId = "PROD001",
            RequiredQuantity = 10
        };

        // Act
        var result = await _inventoryService.CheckInventoryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be("PROD001");
        result.RequiredQuantity.Should().Be(10);
        result.AvailableQuantity.Should().Be(50);
        result.IsAvailable.Should().BeTrue();
        result.Message.Should().Be("Stock available");
    }

    [Fact]
    public async Task CheckInventoryAsync_InsufficientStock_ShouldReturnUnavailable()
    {
        // Arrange
        var request = new CheckInventoryRequest
        {
            ProductId = "PROD001",
            RequiredQuantity = 100 // More than available (50)
        };

        // Act
        var result = await _inventoryService.CheckInventoryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be("PROD001");
        result.RequiredQuantity.Should().Be(100);
        result.AvailableQuantity.Should().Be(50);
        result.IsAvailable.Should().BeFalse();
        result.Message.Should().Be("Insufficient stock");
    }

    [Fact]
    public async Task CheckInventoryAsync_NonExistentProduct_ShouldReturnNotFound()
    {
        // Arrange
        var request = new CheckInventoryRequest
        {
            ProductId = "NONEXISTENT",
            RequiredQuantity = 5
        };

        // Act
        var result = await _inventoryService.CheckInventoryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be("NONEXISTENT");
        result.AvailableQuantity.Should().Be(0);
        result.IsAvailable.Should().BeFalse();
        result.Message.Should().Be("Product not found");
    }

    [Fact]
    public async Task ReserveInventoryAsync_ValidRequest_ShouldCreateReservation()
    {
        // Arrange
        var request = new ReserveInventoryRequest
        {
            ProductId = "PROD001",
            Quantity = 5,
            OrderId = "ORDER123",
            ReservationReason = "Order processing"
        };

        // Act
        var result = await _inventoryService.ReserveInventoryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.ReservationId.Should().BeGreaterThan(0);
        result.ProductId.Should().Be("PROD001");
        result.ReservedQuantity.Should().Be(5);
        result.ReservationExpiry.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        result.Message.Should().Be("Inventory reserved successfully");

        // Verify inventory was updated
        var updatedItem = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        updatedItem!.AvailableQuantity.Should().Be(45); // 50 - 5
        updatedItem.ReservedQuantity.Should().Be(5);

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.IncrementCounter("inventory_reservations", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
        _mockMetrics.Verify(m => m.RecordValue("reserved_quantity", 5.0), Times.Once);
    }

    [Fact]
    public async Task ReserveInventoryAsync_InsufficientStock_ShouldReturnFailure()
    {
        // Arrange
        var request = new ReserveInventoryRequest
        {
            ProductId = "PROD001",
            Quantity = 100, // More than available (50)
            OrderId = "ORDER123"
        };

        // Act
        var result = await _inventoryService.ReserveInventoryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ProductId.Should().Be("PROD001");
        result.Message.Should().Contain("Insufficient stock");
        result.Message.Should().Contain("Available: 50, Requested: 100");

        // Verify inventory was not changed
        var item = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        item!.AvailableQuantity.Should().Be(50);
        item.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task ReserveInventoryAsync_NonExistentProduct_ShouldReturnFailure()
    {
        // Arrange
        var request = new ReserveInventoryRequest
        {
            ProductId = "NONEXISTENT",
            Quantity = 5,
            OrderId = "ORDER123"
        };

        // Act
        var result = await _inventoryService.ReserveInventoryAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ProductId.Should().Be("NONEXISTENT");
        result.Message.Should().Be("Product not found");
    }

    [Fact]
    public async Task ReleaseInventoryAsync_ValidReservation_ShouldReleaseInventory()
    {
        // Arrange - First create a reservation
        var reserveRequest = new ReserveInventoryRequest
        {
            ProductId = "PROD001",
            Quantity = 10,
            OrderId = "ORDER123"
        };
        var reserveResult = await _inventoryService.ReserveInventoryAsync(reserveRequest);

        var releaseRequest = new ReleaseInventoryRequest
        {
            ReservationId = reserveResult.ReservationId,
            Reason = "Order cancelled"
        };

        // Act
        var result = await _inventoryService.ReleaseInventoryAsync(releaseRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeTrue();
        result.ReservationId.Should().Be(reserveResult.ReservationId);
        result.ProductId.Should().Be("PROD001");
        result.ReleasedQuantity.Should().Be(10);
        result.Message.Should().Be("Inventory released successfully");

        // Verify inventory was restored
        var updatedItem = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        updatedItem!.AvailableQuantity.Should().Be(50); // Back to original
        updatedItem.ReservedQuantity.Should().Be(0);

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.IncrementCounter("inventory_releases", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
        _mockMetrics.Verify(m => m.RecordValue("released_quantity", 10.0), Times.Once);
    }

    [Fact]
    public async Task ReleaseInventoryAsync_NonExistentReservation_ShouldReturnFailure()
    {
        // Arrange
        var releaseRequest = new ReleaseInventoryRequest
        {
            ReservationId = 99999,
            Reason = "Test"
        };

        // Act
        var result = await _inventoryService.ReleaseInventoryAsync(releaseRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccessful.Should().BeFalse();
        result.ReservationId.Should().Be(99999);
        result.Message.Should().Be("Reservation not found or already released");
    }

    [Fact]
    public async Task UpdateInventoryAsync_ValidIncrement_ShouldUpdateQuantity()
    {
        // Arrange
        var request = new UpdateInventoryRequest
        {
            ProductId = "PROD001",
            QuantityChange = 25,
            Reason = "Stock replenishment"
        };

        // Act
        var result = await _inventoryService.UpdateInventoryAsync(request);

        // Assert
        result.Should().BeTrue();

        // Verify inventory was updated
        var updatedItem = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        updatedItem!.AvailableQuantity.Should().Be(75); // 50 + 25
        updatedItem.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.IncrementCounter("inventory_updates", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
        _mockMetrics.Verify(m => m.RecordValue("inventory_change", 25.0), Times.Once);
    }

    [Fact]
    public async Task UpdateInventoryAsync_ValidDecrement_ShouldUpdateQuantity()
    {
        // Arrange
        var request = new UpdateInventoryRequest
        {
            ProductId = "PROD001",
            QuantityChange = -10,
            Reason = "Damaged goods removal"
        };

        // Act
        var result = await _inventoryService.UpdateInventoryAsync(request);

        // Assert
        result.Should().BeTrue();

        // Verify inventory was updated
        var updatedItem = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        updatedItem!.AvailableQuantity.Should().Be(40); // 50 - 10
    }

    [Fact]
    public async Task UpdateInventoryAsync_ExcessiveDecrement_ShouldReturnFalse()
    {
        // Arrange
        var request = new UpdateInventoryRequest
        {
            ProductId = "PROD001",
            QuantityChange = -100, // Would result in negative quantity
            Reason = "Test"
        };

        // Act
        var result = await _inventoryService.UpdateInventoryAsync(request);

        // Assert
        result.Should().BeFalse();

        // Verify inventory was not changed
        var item = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        item!.AvailableQuantity.Should().Be(50); // Unchanged
    }

    [Fact]
    public async Task UpdateInventoryAsync_NonExistentProduct_ShouldReturnFalse()
    {
        // Arrange
        var request = new UpdateInventoryRequest
        {
            ProductId = "NONEXISTENT",
            QuantityChange = 10,
            Reason = "Test"
        };

        // Act
        var result = await _inventoryService.UpdateInventoryAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ServiceOperational_ShouldReturnTrue()
    {
        // Act
        var isHealthy = await _inventoryService.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();

        // Verify metrics were recorded
        _mockMetrics.Verify(m => m.IncrementCounter("health_checks", 1, 
            It.IsAny<KeyValuePair<string, object?>[]>()), Times.Once);
    }

    [Fact]
    public async Task MultipleReservations_ShouldGenerateUniqueReservationIds()
    {
        // Arrange
        var request1 = new ReserveInventoryRequest { ProductId = "PROD001", Quantity = 5 };
        var request2 = new ReserveInventoryRequest { ProductId = "PROD002", Quantity = 3 };

        // Act
        var result1 = await _inventoryService.ReserveInventoryAsync(request1);
        var result2 = await _inventoryService.ReserveInventoryAsync(request2);

        // Assert
        result1.ReservationId.Should().NotBe(result2.ReservationId);
        result1.ReservationId.Should().BeLessThan(result2.ReservationId);
        result1.IsSuccessful.Should().BeTrue();
        result2.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task ReserveAndReleaseCycle_ShouldMaintainInventoryConsistency()
    {
        // Arrange
        var originalItem = await _inventoryService.GetInventoryByProductIdAsync("PROD001");
        var originalAvailable = originalItem!.AvailableQuantity;

        var reserveRequest = new ReserveInventoryRequest
        {
            ProductId = "PROD001",
            Quantity = 15,
            OrderId = "ORDER123"
        };

        // Act - Reserve
        var reserveResult = await _inventoryService.ReserveInventoryAsync(reserveRequest);
        var afterReserve = await _inventoryService.GetInventoryByProductIdAsync("PROD001");

        // Act - Release
        var releaseRequest = new ReleaseInventoryRequest
        {
            ReservationId = reserveResult.ReservationId,
            Reason = "Order cancelled"
        };
        var releaseResult = await _inventoryService.ReleaseInventoryAsync(releaseRequest);
        var afterRelease = await _inventoryService.GetInventoryByProductIdAsync("PROD001");

        // Assert
        reserveResult.IsSuccessful.Should().BeTrue();
        afterReserve!.AvailableQuantity.Should().Be(originalAvailable - 15);
        afterReserve.ReservedQuantity.Should().Be(15);

        releaseResult.IsSuccessful.Should().BeTrue();
        afterRelease!.AvailableQuantity.Should().Be(originalAvailable);
        afterRelease.ReservedQuantity.Should().Be(0);
    }

    [Theory]
    [InlineData("PROD001", "Gaming Laptop")]
    [InlineData("PROD002", "Mechanical Keyboard")]
    [InlineData("PROD003", "Wireless Mouse")]
    [InlineData("PROD004", "4K Monitor")]
    [InlineData("PROD005", "USB-C Hub")]
    public async Task GetInventoryByProductIdAsync_AllSampleProducts_ShouldExist(string productId, string expectedName)
    {
        // Act
        var item = await _inventoryService.GetInventoryByProductIdAsync(productId);

        // Assert
        item.Should().NotBeNull();
        item!.ProductId.Should().Be(productId);
        item.ProductName.Should().Be(expectedName);
        item.AvailableQuantity.Should().BeGreaterThan(0);
        item.Price.Should().BeGreaterThan(0);
    }
} 