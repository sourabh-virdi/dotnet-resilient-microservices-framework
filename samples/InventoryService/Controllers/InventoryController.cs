using Microsoft.AspNetCore.Mvc;
using InventoryService.Models;
using InventoryService.Services;
using ResilientMicroservices.Core;
using ResilientMicroservices.Core.HealthChecks;
using ResilientMicroservices.Resilience;
using System.Diagnostics;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IDistributedTracing _tracing;
    private readonly IMessageBus _messageBus;
    private readonly IMetricsCollector _metrics;
    private readonly IRetryPolicyService _retryPolicy;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IInventoryService inventoryService,
        IDistributedTracing tracing,
        IMessageBus messageBus,
        IMetricsCollector metrics,
        IRetryPolicyService retryPolicy,
        ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _tracing = tracing;
        _messageBus = messageBus;
        _metrics = metrics;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAllInventory()
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("GetAllInventory");

        try
        {
            var inventory = await _inventoryService.GetAllInventoryAsync();
            
            stopwatch.Stop();
            _metrics.RecordHttpRequest("GET", "/api/inventory", 200, stopwatch.Elapsed);

            _logger.LogInformation("Retrieved {Count} inventory items", inventory.Count());

            return Ok(inventory);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/inventory", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to retrieve inventory");
            
            return StatusCode(500, new { message = "Failed to retrieve inventory", error = ex.Message });
        }
    }

    [HttpGet("{productId}")]
    public async Task<ActionResult<InventoryItem>> GetInventoryByProductId(string productId)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("GetInventoryByProductId");
        _tracing.AddTag("product_id", productId);

        try
        {
            var item = await _inventoryService.GetInventoryByProductIdAsync(productId);
            
            stopwatch.Stop();

            if (item == null)
            {
                _metrics.RecordHttpRequest("GET", "/api/inventory/{productId}", 404, stopwatch.Elapsed);
                _logger.LogWarning("Product {ProductId} not found", productId);
                return NotFound(new { message = $"Product {productId} not found" });
            }

            _metrics.RecordHttpRequest("GET", "/api/inventory/{productId}", 200, stopwatch.Elapsed);
            return Ok(item);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/inventory/{productId}", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to retrieve inventory for product {ProductId}", productId);
            
            return StatusCode(500, new { message = "Failed to retrieve inventory", error = ex.Message });
        }
    }

    [HttpPost("check")]
    public async Task<ActionResult<CheckInventoryResponse>> CheckInventory([FromBody] CheckInventoryRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("CheckInventory");
        _tracing.AddTag("product_id", request.ProductId);
        _tracing.AddTag("required_quantity", request.RequiredQuantity.ToString());

        try
        {
            _logger.LogInformation("Checking inventory for {ProductId}, quantity: {Quantity}", 
                request.ProductId, request.RequiredQuantity);

            var result = await _inventoryService.CheckInventoryAsync(request);
            
            stopwatch.Stop();
            _metrics.RecordHttpRequest("POST", "/api/inventory/check", 200, stopwatch.Elapsed);

            _tracing.AddEvent("InventoryChecked", tags: new[]
            {
                new KeyValuePair<string, object?>("product_id", request.ProductId),
                new KeyValuePair<string, object?>("is_available", result.IsAvailable),
                new KeyValuePair<string, object?>("available_quantity", result.AvailableQuantity)
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("POST", "/api/inventory/check", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to check inventory for product {ProductId}", request.ProductId);
            
            return StatusCode(500, new { message = "Failed to check inventory", error = ex.Message });
        }
    }

    [HttpPost("reserve")]
    public async Task<ActionResult<ReserveInventoryResponse>> ReserveInventory([FromBody] ReserveInventoryRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("ReserveInventory");
        _tracing.AddTag("product_id", request.ProductId);
        _tracing.AddTag("quantity", request.Quantity.ToString());
        _tracing.AddTag("order_id", request.OrderId ?? "unknown");

        try
        {
            _logger.LogInformation("Reserving inventory for {ProductId}, quantity: {Quantity}, order: {OrderId}", 
                request.ProductId, request.Quantity, request.OrderId);

            var result = await _retryPolicy.ExecuteWithRetryAsync(
                async (ct) => await _inventoryService.ReserveInventoryAsync(request),
                CancellationToken.None);

            stopwatch.Stop();
            _metrics.RecordHttpRequest("POST", "/api/inventory/reserve", 200, stopwatch.Elapsed);

            // Publish inventory reserved event if successful
            if (result.IsSuccessful)
            {
                var inventoryEvent = new InventoryReservedEvent
                {
                    ReservationId = result.ReservationId,
                    ProductId = result.ProductId,
                    ReservedQuantity = result.ReservedQuantity,
                    OrderId = request.OrderId,
                    ReservationExpiry = result.ReservationExpiry
                };

                await _messageBus.PublishAsync(inventoryEvent, "inventory.reserved");

                _tracing.AddEvent("InventoryReserved", tags: new[]
                {
                    new KeyValuePair<string, object?>("reservation_id", result.ReservationId),
                    new KeyValuePair<string, object?>("product_id", result.ProductId),
                    new KeyValuePair<string, object?>("quantity", result.ReservedQuantity)
                });

                _logger.LogInformation("Inventory reserved successfully: {ReservationId}", result.ReservationId);
            }
            else
            {
                _logger.LogWarning("Inventory reservation failed for {ProductId}: {Message}", 
                    request.ProductId, result.Message);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("POST", "/api/inventory/reserve", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to reserve inventory for product {ProductId}", request.ProductId);
            
            return StatusCode(500, new { message = "Failed to reserve inventory", error = ex.Message });
        }
    }

    [HttpPost("release")]
    public async Task<ActionResult<ReleaseInventoryResponse>> ReleaseInventory([FromBody] ReleaseInventoryRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("ReleaseInventory");
        _tracing.AddTag("reservation_id", request.ReservationId.ToString());

        try
        {
            _logger.LogInformation("Releasing inventory reservation {ReservationId}", request.ReservationId);

            var result = await _retryPolicy.ExecuteWithRetryAsync(
                async (ct) => await _inventoryService.ReleaseInventoryAsync(request),
                CancellationToken.None);

            stopwatch.Stop();
            _metrics.RecordHttpRequest("POST", "/api/inventory/release", 200, stopwatch.Elapsed);

            // Publish inventory released event if successful
            if (result.IsSuccessful)
            {
                var inventoryEvent = new InventoryReleasedEvent
                {
                    ReservationId = result.ReservationId,
                    ProductId = result.ProductId,
                    ReleasedQuantity = result.ReleasedQuantity,
                    Reason = request.Reason
                };

                await _messageBus.PublishAsync(inventoryEvent, "inventory.released");

                _tracing.AddEvent("InventoryReleased", tags: new[]
                {
                    new KeyValuePair<string, object?>("reservation_id", result.ReservationId),
                    new KeyValuePair<string, object?>("product_id", result.ProductId),
                    new KeyValuePair<string, object?>("quantity", result.ReleasedQuantity)
                });

                _logger.LogInformation("Inventory released successfully: {ReservationId}", result.ReservationId);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("POST", "/api/inventory/release", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to release inventory reservation {ReservationId}", request.ReservationId);
            
            return StatusCode(500, new { message = "Failed to release inventory", error = ex.Message });
        }
    }

    [HttpPut("update")]
    public async Task<ActionResult> UpdateInventory([FromBody] UpdateInventoryRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        using var activity = _tracing.StartActivity("UpdateInventory");
        _tracing.AddTag("product_id", request.ProductId);
        _tracing.AddTag("quantity_change", request.QuantityChange.ToString());

        try
        {
            _logger.LogInformation("Updating inventory for {ProductId}, change: {Change}", 
                request.ProductId, request.QuantityChange);

            var success = await _inventoryService.UpdateInventoryAsync(request);
            
            stopwatch.Stop();

            if (success)
            {
                _metrics.RecordHttpRequest("PUT", "/api/inventory/update", 200, stopwatch.Elapsed);
                
                _tracing.AddEvent("InventoryUpdated", tags: new[]
                {
                    new KeyValuePair<string, object?>("product_id", request.ProductId),
                    new KeyValuePair<string, object?>("quantity_change", request.QuantityChange)
                });

                return Ok(new { message = "Inventory updated successfully" });
            }
            else
            {
                _metrics.RecordHttpRequest("PUT", "/api/inventory/update", 400, stopwatch.Elapsed);
                return BadRequest(new { message = "Failed to update inventory" });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("PUT", "/api/inventory/update", 500, stopwatch.Elapsed);
            _tracing.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "Failed to update inventory for product {ProductId}", request.ProductId);
            
            return StatusCode(500, new { message = "Failed to update inventory", error = ex.Message });
        }
    }

    [HttpGet("health")]
    public async Task<ActionResult> HealthCheck()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var isHealthy = await _inventoryService.IsHealthyAsync();
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/inventory/health", 200, stopwatch.Elapsed);

            return Ok(new
            {
                status = isHealthy ? "healthy" : "unhealthy",
                service = "InventoryService",
                timestamp = DateTime.UtcNow,
                duration_ms = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _metrics.RecordHttpRequest("GET", "/api/inventory/health", 500, stopwatch.Elapsed);
            _logger.LogError(ex, "Health check failed");
            
            return StatusCode(500, new { status = "unhealthy", error = ex.Message });
        }
    }
} 