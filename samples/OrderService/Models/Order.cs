namespace OrderService.Models;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    PaymentProcessed,
    InventoryReserved,
    Completed,
    Cancelled
}

public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
} 