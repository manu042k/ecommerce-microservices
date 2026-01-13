using OrderService.Data;
using OrderService.Dtos;
using OrderService.Models;
using OrderService.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OrderService.Tests.Controllers;

public class OrdersControllerTests : IClassFixture<OrderServiceApplicationFactory>
{
    private readonly OrderServiceApplicationFactory _factory;

    public OrdersControllerTests(OrderServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMyOrders_ReturnsUserOrders()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await SeedOrderAsync(context, userId: "testuser");
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders/my-orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.Should().NotBeNull();
        orders!.Should().Contain(o => o.Id == order.Id);
    }

    [Fact]
    public async Task GetOrderById_WithValidId_ReturnsOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await SeedOrderAsync(context, userId: "testuser");
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetOrderById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_CreatesOrder()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateOrderRequest
        {
            Items = new List<OrderItemRequest>
            {
                new OrderItemRequest
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 2
                }
            },
            ShippingAddress = "123 Test St, Test City"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        order.Should().NotBeNull();
        order!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithValidRequest_UpdatesStatus()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await SeedOrderAsync(context);
        
        var client = _factory.CreateClient();
        var request = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Processing
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/orders/{order.Id}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse!.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task GetOrdersByStatus_ReturnsFilteredOrders()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await SeedOrderAsync(context, status: OrderStatus.Pending);
        await SeedOrderAsync(context, status: OrderStatus.Processing);
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders/by-status/Pending");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.Should().NotBeNull();
        orders!.All(o => o.Status == OrderStatus.Pending).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOrder_WithValidId_DeletesOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await SeedOrderAsync(context);
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/api/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteOrder_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/api/orders/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Order> SeedOrderAsync(
        OrderDbContext context,
        string userId = "testuser",
        OrderStatus status = OrderStatus.Pending)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = status,
            TotalAmount = 99.99m,
            ShippingAddress = "123 Test St",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "Test Product",
            UnitPrice = 49.99m,
            Quantity = 2
        });

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }
}
