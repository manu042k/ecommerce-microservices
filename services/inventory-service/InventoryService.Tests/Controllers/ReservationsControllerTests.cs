using InventoryService.Data;
using InventoryService.Dtos;
using InventoryService.Models;
using InventoryService.Services;
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

public class ReservationsControllerTests : IClassFixture<InventoryServiceApplicationFactory>
{
    private readonly InventoryServiceApplicationFactory _factory;

    public ReservationsControllerTests(InventoryServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateReservation_WithValidRequest_CreatesReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context, quantityOnHand: 100);
        
        var client = _factory.CreateClient();
        var request = new CreateReservationRequest
        {
            OrderId = Guid.NewGuid(),
            Items = new List<ReservationItemRequest>
            {
                new ReservationItemRequest
                {
                    ProductId = inventoryItem.ProductId,
                    Quantity = 10
                }
            },
            HoldMinutes = 15
        };

        // Act
        var response = await client.PostAsJsonAsync("/internal/inventory/reservations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        reservation.Should().NotBeNull();
        reservation!.OrderId.Should().Be(request.OrderId);
        reservation.Status.Should().Be(InventoryReservationStatus.Pending); // Reservations are created with Pending status
    }

    [Fact]
    public async Task CreateReservation_WithInsufficientStock_ReturnsConflict()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context, quantityOnHand: 5);
        
        var client = _factory.CreateClient();
        var request = new CreateReservationRequest
        {
            OrderId = Guid.NewGuid(),
            Items = new List<ReservationItemRequest>
            {
                new ReservationItemRequest
                {
                    ProductId = inventoryItem.ProductId,
                    Quantity = 100 // More than available
                }
            },
            HoldMinutes = 15
        };

        // Act
        var response = await client.PostAsJsonAsync("/internal/inventory/reservations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReleaseReservation_WithValidId_ReleasesReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context);
        
        var client = _factory.CreateClient();
        // Create reservation via HTTP to ensure it's in the same database context
        var createRequest = new CreateReservationRequest
        {
            OrderId = Guid.NewGuid(),
            Items = new List<ReservationItemRequest>
            {
                new ReservationItemRequest
                {
                    ProductId = inventoryItem.ProductId,
                    Quantity = 10
                }
            },
            HoldMinutes = 15
        };
        var createResponse = await client.PostAsJsonAsync("/internal/inventory/reservations", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>();
        reservation.Should().NotBeNull();
        
        var request = new ReleaseReservationRequest { Reason = "Test release" };

        // Act
        var response = await client.PostAsJsonAsync($"/internal/inventory/reservations/{reservation!.Id}/release", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ReleaseReservation_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync($"/internal/inventory/reservations/{Guid.NewGuid()}/release", new ReleaseReservationRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CommitReservation_WithValidId_CommitsReservation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context);
        
        var client = _factory.CreateClient();
        // Create reservation via HTTP to ensure it's in the same database context
        var createRequest = new CreateReservationRequest
        {
            OrderId = Guid.NewGuid(),
            Items = new List<ReservationItemRequest>
            {
                new ReservationItemRequest
                {
                    ProductId = inventoryItem.ProductId,
                    Quantity = 10
                }
            },
            HoldMinutes = 15
        };
        var createResponse = await client.PostAsJsonAsync("/internal/inventory/reservations", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservation = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>();
        reservation.Should().NotBeNull();

        // Act
        var response = await client.PostAsync($"/internal/inventory/reservations/{reservation!.Id}/commit", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CommitReservation_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/internal/inventory/reservations/{Guid.NewGuid()}/commit", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvailability_WithValidRequest_ReturnsAvailability()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var inventoryItem = await SeedInventoryItemAsync(context, quantityOnHand: 100, quantityReserved: 20);
        
        var client = _factory.CreateClient();
        var request = new AvailabilityRequest
        {
            ProductIds = new List<Guid> { inventoryItem.ProductId }
        };

        // Act
        var response = await client.PostAsJsonAsync("/internal/inventory/reservations/availability", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var availability = await response.Content.ReadFromJsonAsync<List<AvailabilityEntryDto>>();
        availability.Should().NotBeNull();
        availability!.Should().HaveCount(1);
        availability!.First().ProductId.Should().Be(inventoryItem.ProductId);
        availability!.First().AvailableQuantity.Should().Be(80);
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

    private static async Task<ReservationResponse> CreateReservationAsync(
        IInventoryService inventoryService,
        Guid productId,
        int quantity)
    {
        var request = new CreateReservationRequest
        {
            OrderId = Guid.NewGuid(),
            Items = new List<ReservationItemRequest>
            {
                new ReservationItemRequest
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            },
            HoldMinutes = 15
        };

        return await inventoryService.CreateReservationAsync(request);
    }
}
