using OrderService.Services;
using OrderService.Dtos;
using OrderService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Get all orders for the current user
    /// </summary>
    [HttpGet("my-orders")]
    public async Task<ActionResult<List<OrderResponse>>> GetMyOrders()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        _logger.LogInformation("Fetching orders for user {UserId}", userId);
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    /// <summary>
    /// Get a specific order by ID
    /// </summary>
    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderResponse>> GetOrderById(Guid orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return NotFound($"Order {orderId} not found");
        }

        // Verify user owns this order
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (order.UserId != userId && !User.IsInRole("Admin"))
        {
            _logger.LogWarning("User {UserId} attempted unauthorized access to order {OrderId}", userId, orderId);
            return Forbid();
        }

        return Ok(order);
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        try
        {
            _logger.LogInformation("Creating new order for user {UserId} with {ItemCount} items", userId, request.Items.Count);
            var order = await _orderService.CreateOrderAsync(userId, request);
            return CreatedAtAction(nameof(GetOrderById), new { orderId = order.Id }, order);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid order request for user {UserId}", userId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Order creation failed for user {UserId}", userId);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update order status (admin only)
    /// </summary>
    [HttpPut("{orderId}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(
        Guid orderId,
        [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            _logger.LogInformation("Updating order {OrderId} status to {NewStatus}", orderId, request.Status);
            var order = await _orderService.UpdateOrderStatusAsync(orderId, request.Status);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for status update", orderId);
                return NotFound($"Order {orderId} not found");
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status", orderId);
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get orders by status (admin only)
    /// </summary>
    [HttpGet("by-status/{status}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<OrderResponse>>> GetOrdersByStatus(OrderStatus status)
    {
        _logger.LogInformation("Fetching orders with status {Status}", status);
        var orders = await _orderService.GetOrdersByStatusAsync(status);
        return Ok(orders);
    }

    /// <summary>
    /// Delete an order (admin only)
    /// </summary>
    [HttpDelete("{orderId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrder(Guid orderId)
    {
        var success = await _orderService.DeleteOrderAsync(orderId);
        if (!success)
        {
            _logger.LogWarning("Order {OrderId} not found for deletion", orderId);
            return NotFound($"Order {orderId} not found");
        }

        return NoContent();
    }
}
