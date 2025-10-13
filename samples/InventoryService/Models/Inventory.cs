namespace InventoryService.Models;

public class InventoryItem
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int TotalQuantity => AvailableQuantity + ReservedQuantity;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class ReserveInventoryRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? ReservationReason { get; set; }
    public string? OrderId { get; set; }
}

public class ReserveInventoryResponse
{
    public int ReservationId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int ReservedQuantity { get; set; }
    public DateTime ReservationExpiry { get; set; }
    public bool IsSuccessful { get; set; }
    public string? Message { get; set; }
}

public class ReleaseInventoryRequest
{
    public int ReservationId { get; set; }
    public string? Reason { get; set; }
}

public class ReleaseInventoryResponse
{
    public int ReservationId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int ReleasedQuantity { get; set; }
    public bool IsSuccessful { get; set; }
    public string? Message { get; set; }
}

public class UpdateInventoryRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int QuantityChange { get; set; } // Positive for additions, negative for removals
    public string? Reason { get; set; }
}

public class CheckInventoryRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
}

public class CheckInventoryResponse
{
    public string ProductId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int RequiredQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public string? Message { get; set; }
}

public class InventoryReservation
{
    public int Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int ReservedQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? OrderId { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
}

// Events
public class InventoryReservedEvent : ResilientMicroservices.Core.BaseMessage
{
    public int ReservationId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int ReservedQuantity { get; set; }
    public string? OrderId { get; set; }
    public DateTime ReservationExpiry { get; set; }
}

public class InventoryReleasedEvent : ResilientMicroservices.Core.BaseMessage
{
    public int ReservationId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int ReleasedQuantity { get; set; }
    public string? OrderId { get; set; }
    public string? Reason { get; set; }
} 