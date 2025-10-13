using InventoryService.Models;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<IEnumerable<InventoryItem>> GetAllInventoryAsync();
    Task<InventoryItem?> GetInventoryByProductIdAsync(string productId);
    Task<CheckInventoryResponse> CheckInventoryAsync(CheckInventoryRequest request);
    Task<ReserveInventoryResponse> ReserveInventoryAsync(ReserveInventoryRequest request);
    Task<ReleaseInventoryResponse> ReleaseInventoryAsync(ReleaseInventoryRequest request);
    Task<bool> UpdateInventoryAsync(UpdateInventoryRequest request);
    Task<bool> IsHealthyAsync();
}

public class InventoryServiceImpl : IInventoryService
{
    private readonly ILogger<InventoryServiceImpl> _logger;
    private readonly ResilientMicroservices.Core.IMetricsCollector _metrics;
    private readonly List<InventoryItem> _inventory = new();
    private readonly List<InventoryReservation> _reservations = new();
    private readonly Random _random = new();
    private int _nextReservationId = 1;

    public InventoryServiceImpl(ILogger<InventoryServiceImpl> logger, ResilientMicroservices.Core.IMetricsCollector metrics)
    {
        _logger = logger;
        _metrics = metrics;
        InitializeSampleData();
    }

    private void InitializeSampleData()
    {
        _inventory.AddRange(new[]
        {
            new InventoryItem { Id = 1, ProductId = "PROD001", ProductName = "Gaming Laptop", AvailableQuantity = 50, Price = 1299.99m, Category = "Electronics" },
            new InventoryItem { Id = 2, ProductId = "PROD002", ProductName = "Mechanical Keyboard", AvailableQuantity = 150, Price = 129.99m, Category = "Accessories" },
            new InventoryItem { Id = 3, ProductId = "PROD003", ProductName = "Wireless Mouse", AvailableQuantity = 200, Price = 79.99m, Category = "Accessories" },
            new InventoryItem { Id = 4, ProductId = "PROD004", ProductName = "4K Monitor", AvailableQuantity = 75, Price = 399.99m, Category = "Electronics" },
            new InventoryItem { Id = 5, ProductId = "PROD005", ProductName = "USB-C Hub", AvailableQuantity = 100, Price = 49.99m, Category = "Accessories" }
        });

        foreach (var item in _inventory)
        {
            item.LastUpdated = DateTime.UtcNow;
        }
    }

    public Task<IEnumerable<InventoryItem>> GetAllInventoryAsync()
    {
        return Task.FromResult<IEnumerable<InventoryItem>>(_inventory);
    }

    public Task<InventoryItem?> GetInventoryByProductIdAsync(string productId)
    {
        var item = _inventory.FirstOrDefault(i => i.ProductId == productId);
        return Task.FromResult(item);
    }

    public Task<CheckInventoryResponse> CheckInventoryAsync(CheckInventoryRequest request)
    {
        var item = _inventory.FirstOrDefault(i => i.ProductId == request.ProductId);
        
        if (item == null)
        {
            return Task.FromResult(new CheckInventoryResponse
            {
                ProductId = request.ProductId,
                RequiredQuantity = request.RequiredQuantity,
                AvailableQuantity = 0,
                IsAvailable = false,
                Message = "Product not found"
            });
        }

        var isAvailable = item.AvailableQuantity >= request.RequiredQuantity;
        
        return Task.FromResult(new CheckInventoryResponse
        {
            ProductId = request.ProductId,
            RequiredQuantity = request.RequiredQuantity,
            AvailableQuantity = item.AvailableQuantity,
            IsAvailable = isAvailable,
            Message = isAvailable ? "Stock available" : "Insufficient stock"
        });
    }

    public async Task<ReserveInventoryResponse> ReserveInventoryAsync(ReserveInventoryRequest request)
    {
        await Task.Delay(100); // Simulate processing

        var item = _inventory.FirstOrDefault(i => i.ProductId == request.ProductId);
        
        if (item == null)
        {
            _metrics.IncrementCounter("inventory_reservations", 1, 
                new KeyValuePair<string, object?>("status", "failed"),
                new KeyValuePair<string, object?>("reason", "product_not_found"));
            
            return new ReserveInventoryResponse
            {
                ProductId = request.ProductId,
                IsSuccessful = false,
                Message = "Product not found"
            };
        }

        if (item.AvailableQuantity < request.Quantity)
        {
            _metrics.IncrementCounter("inventory_reservations", 1, 
                new KeyValuePair<string, object?>("status", "failed"),
                new KeyValuePair<string, object?>("reason", "insufficient_stock"));
            
            return new ReserveInventoryResponse
            {
                ProductId = request.ProductId,
                IsSuccessful = false,
                Message = $"Insufficient stock. Available: {item.AvailableQuantity}, Requested: {request.Quantity}"
            };
        }

        // Create reservation
        var reservation = new InventoryReservation
        {
            Id = _nextReservationId++,
            ProductId = request.ProductId,
            ReservedQuantity = request.Quantity,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30), // 30 minute expiry
            OrderId = request.OrderId,
            Reason = request.ReservationReason
        };

        _reservations.Add(reservation);

        // Update inventory
        item.AvailableQuantity -= request.Quantity;
        item.ReservedQuantity += request.Quantity;
        item.LastUpdated = DateTime.UtcNow;

        _metrics.IncrementCounter("inventory_reservations", 1, 
            new KeyValuePair<string, object?>("status", "success"),
            new KeyValuePair<string, object?>("product_id", request.ProductId));
        
        _metrics.RecordValue("reserved_quantity", request.Quantity);

        _logger.LogInformation("Reserved {Quantity} of {ProductId} (Reservation: {ReservationId})", 
            request.Quantity, request.ProductId, reservation.Id);

        return new ReserveInventoryResponse
        {
            ReservationId = reservation.Id,
            ProductId = request.ProductId,
            ReservedQuantity = request.Quantity,
            ReservationExpiry = reservation.ExpiresAt,
            IsSuccessful = true,
            Message = "Inventory reserved successfully"
        };
    }

    public async Task<ReleaseInventoryResponse> ReleaseInventoryAsync(ReleaseInventoryRequest request)
    {
        await Task.Delay(50); // Simulate processing

        var reservation = _reservations.FirstOrDefault(r => r.Id == request.ReservationId && r.IsActive);
        
        if (reservation == null)
        {
            _metrics.IncrementCounter("inventory_releases", 1, 
                new KeyValuePair<string, object?>("status", "failed"),
                new KeyValuePair<string, object?>("reason", "reservation_not_found"));
            
            return new ReleaseInventoryResponse
            {
                ReservationId = request.ReservationId,
                IsSuccessful = false,
                Message = "Reservation not found or already released"
            };
        }

        var item = _inventory.FirstOrDefault(i => i.ProductId == reservation.ProductId);
        if (item != null)
        {
            // Release inventory
            item.AvailableQuantity += reservation.ReservedQuantity;
            item.ReservedQuantity -= reservation.ReservedQuantity;
            item.LastUpdated = DateTime.UtcNow;
        }

        // Mark reservation as inactive
        reservation.IsActive = false;

        _metrics.IncrementCounter("inventory_releases", 1, 
            new KeyValuePair<string, object?>("status", "success"),
            new KeyValuePair<string, object?>("product_id", reservation.ProductId));
        
        _metrics.RecordValue("released_quantity", reservation.ReservedQuantity);

        _logger.LogInformation("Released {Quantity} of {ProductId} (Reservation: {ReservationId})", 
            reservation.ReservedQuantity, reservation.ProductId, reservation.Id);

        return new ReleaseInventoryResponse
        {
            ReservationId = reservation.Id,
            ProductId = reservation.ProductId,
            ReleasedQuantity = reservation.ReservedQuantity,
            IsSuccessful = true,
            Message = "Inventory released successfully"
        };
    }

    public Task<bool> UpdateInventoryAsync(UpdateInventoryRequest request)
    {
        var item = _inventory.FirstOrDefault(i => i.ProductId == request.ProductId);
        
        if (item == null)
        {
            _logger.LogWarning("Attempted to update non-existent product: {ProductId}", request.ProductId);
            return Task.FromResult(false);
        }

        var newQuantity = item.AvailableQuantity + request.QuantityChange;
        if (newQuantity < 0)
        {
            _logger.LogWarning("Cannot update inventory for {ProductId}: would result in negative quantity", request.ProductId);
            return Task.FromResult(false);
        }

        item.AvailableQuantity = newQuantity;
        item.LastUpdated = DateTime.UtcNow;

        _metrics.IncrementCounter("inventory_updates", 1, 
            new KeyValuePair<string, object?>("product_id", request.ProductId),
            new KeyValuePair<string, object?>("change_type", request.QuantityChange > 0 ? "increase" : "decrease"));
        
        _metrics.RecordValue("inventory_change", Math.Abs(request.QuantityChange));

        _logger.LogInformation("Updated inventory for {ProductId}: {Change} (New quantity: {NewQuantity})", 
            request.ProductId, request.QuantityChange, item.AvailableQuantity);

        return Task.FromResult(true);
    }

    public Task<bool> IsHealthyAsync()
    {
        var isHealthy = _inventory.Count > 0 && _reservations.Count < 1000;
        
        _metrics.IncrementCounter("health_checks", 1, 
            new KeyValuePair<string, object?>("service", "inventory"),
            new KeyValuePair<string, object?>("status", isHealthy ? "healthy" : "unhealthy"));

        return Task.FromResult(isHealthy);
    }
} 