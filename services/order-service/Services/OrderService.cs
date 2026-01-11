using OrderService.Dtos;
using OrderService.Models;
using OrderService.Data;
using OrderService.Contracts;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace OrderService.Services;

public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderService> _logger;
    private const string CatalogServiceUrl = "http://catalog-service:8080"; // Will be configured via settings

    public OrderService(
        OrderDbContext context,
        IHttpClientFactory httpClientFactory,
        IPublishEndpoint publishEndpoint,
        ILogger<OrderService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return null;

        return MapToOrderResponse(order);
    }

    public async Task<List<OrderResponse>> GetUserOrdersAsync(string userId)
    {
        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToOrderResponse).ToList();
    }

    public async Task<OrderResponse> CreateOrderAsync(string userId, CreateOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("Order must contain at least one item");

        // Verify products exist in catalog service
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var productsData = await VerifyProductsExistAsync(productIds);

        if (productsData.Count != productIds.Count)
            throw new InvalidOperationException("One or more products do not exist");

        // Create order
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            ShippingAddress = request.ShippingAddress,
            CreatedAt = DateTime.UtcNow
        };

        // Add order items
        foreach (var itemRequest in request.Items)
        {
            var productData = productsData.FirstOrDefault(p => p.Id == itemRequest.ProductId);
            if (productData == null)
                throw new InvalidOperationException($"Product {itemRequest.ProductId} not found");

            var orderItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = itemRequest.ProductId,
                ProductName = productData.Name,
                UnitPrice = productData.Price,
                Quantity = itemRequest.Quantity
            };

            order.Items.Add(orderItem);
        }

        order.TotalAmount = order.Items.Sum(i => i.Subtotal);

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created for user {UserId} with {ItemCount} items",
            order.Id, userId, order.Items.Count);

        // Publish event
        await _publishEndpoint.Publish<IOrderCreated>(new
        {
            OrderId = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount,
            Timestamp = DateTime.UtcNow
        });

        return MapToOrderResponse(order);
    }

    public async Task<OrderResponse?> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return null;

        var oldStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        _context.Orders.Update(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} status updated from {OldStatus} to {NewStatus}",
            orderId, oldStatus, newStatus);

        // Publish event
        await _publishEndpoint.Publish<IOrderStatusChanged>(new
        {
            OrderId = order.Id,
            PreviousStatus = (int)oldStatus,
            NewStatus = (int)newStatus,
            Timestamp = DateTime.UtcNow
        });

        return MapToOrderResponse(order);
    }

    public async Task<List<OrderResponse>> GetOrdersByStatusAsync(OrderStatus status)
    {
        var orders = await _context.Orders
            .Where(o => o.Status == status)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToOrderResponse).ToList();
    }

    public async Task<bool> DeleteOrderAsync(Guid orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return false;

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} deleted", orderId);
        return true;
    }

    private async Task<List<ProductData>> VerifyProductsExistAsync(List<Guid> productIds)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var queryString = string.Join("&", productIds.Select(id => $"ids={id}"));
            var response = await client.GetAsync($"{CatalogServiceUrl}/api/products/verify?{queryString}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to verify products from catalog service");
                return new List<ProductData>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var products = System.Text.Json.JsonSerializer.Deserialize<List<ProductData>>(content,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return products ?? new List<ProductData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying products from catalog service");
            return new List<ProductData>();
        }
    }

    private OrderResponse MapToOrderResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            UserId = order.UserId,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            ShippingAddress = order.ShippingAddress,
            Items = order.Items.Select(item => new OrderItemResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                Subtotal = item.Subtotal
            }).ToList()
        };
    }

    private class ProductData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
