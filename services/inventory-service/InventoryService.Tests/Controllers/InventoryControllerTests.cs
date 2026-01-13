using InventoryService.Data;
using InventoryService.Dtos;
using InventoryService.Models;
using InventoryService.Tests.Support;
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

namespace InventoryService.Tests.Controllers;

public class InventoryControllerTests : IClassFixture<InventoryServiceApplicationFactory>
{
    private readonly InventoryServiceApplicationFactory _factory;

    public InventoryControllerTests(InventoryServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetInventory_ReturnsAllInventoryItems()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context);
        // Ensure changes are saved
        await context.SaveChangesAsync();
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var inventory = await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>();
        inventory.Should().NotBeNull();
        // Verify the item we created is in the list
        inventory!.Should().Contain(i => i.ProductId == inventoryItem.ProductId);
    }

    [Fact]
    public async Task AdjustInventory_WithValidRequest_AdjustsInventory()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context);
        
        var client = _factory.CreateClient();
        var request = new InventoryAdjustmentRequest
        {
            ProductId = inventoryItem.ProductId,
            ProductName = inventoryItem.ProductName,
            Sku = inventoryItem.Sku,
            QuantityDelta = 10,
            Reason = "Test adjustment"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/inventory/adjustments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        result.Should().NotBeNull();
        result!.ProductId.Should().Be(inventoryItem.ProductId);
        result!.QuantityOnHand.Should().Be(inventoryItem.QuantityOnHand + 10);
    }

    [Fact]
    public async Task AdjustInventory_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        
        var client = _factory.CreateClient();
        var request = new InventoryAdjustmentRequest
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Non-existent Product",
            Sku = "SKU999",
            QuantityDelta = -1000, // This will cause an error if quantity goes negative
            Reason = "Invalid adjustment"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/inventory/adjustments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<InventoryItem> SeedInventoryItemAsync(
        InventoryDbContext context,
        Guid? productId = null,
        int quantityOnHand = 100,
        int quantityReserved = 0)
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId ?? Guid.NewGuid(),
            ProductName = "Test Product",
            Sku = "SKU001",
            QuantityOnHand = quantityOnHand,
            QuantityReserved = quantityReserved,
            ReorderPoint = 10,
            SafetyStock = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }
}
