using FulfillmentService.Controllers;
using FulfillmentService.Data;
using FulfillmentService.Dtos;
using FulfillmentService.Models;
using FulfillmentService.Services;
using FulfillmentService.Services.Providers;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Xunit;
using FluentAssertions; 


namespace FulfillmentService.Tests.Controllers;

public class ShipmentsControllerTests
{
    [Fact]
    public async Task CreateShipment_WithValidRequest_CreatesShipment()
    {
        // Arrange
        var context = CreateContext();
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);
        var request = new CreateShipmentRequest
        {
            OrderId = Guid.NewGuid(),
            UserId = "user123",
            Destination = new ShipmentAddressDto
            {
                RecipientName = "John Doe",
                Line1 = "123 Main St",
                City = "New York",
                State = "NY",
                PostalCode = "10001",
                Country = "USA"
            },
            Items = new List<ShipmentItemRequest>
            {
                new ShipmentItemRequest
                {
                    ProductId = Guid.NewGuid(),
                    Sku = "SKU001",
                    Name = "Test Product",
                    Quantity = 1,
                    Weight = 0.5m
                }
            },
            DeclaredValue = 99.99m,
            TotalWeight = 0.5m
        };

        // Act
        var result = await controller.CreateShipment(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdAtResult = result.Result as CreatedAtActionResult;
        createdAtResult!.Value.Should().BeOfType<ShipmentDto>();
        var shipment = createdAtResult.Value as ShipmentDto;
        shipment!.OrderId.Should().Be(request.OrderId);
        shipment.Status.Should().Be(ShipmentStatus.PendingDetails);
    }

    [Fact]
    public async Task GetShipment_WithValidId_ReturnsShipment()
    {
        // Arrange
        var context = CreateContext();
        var shipment = await SeedShipmentAsync(context);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);

        // Act
        var result = await controller.GetShipment(shipment.Id);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(shipment.Id);
    }

    [Fact]
    public async Task GetShipment_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var context = CreateContext();
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);

        // Act
        var result = await controller.GetShipment(Guid.NewGuid());

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetShipments_WithOrderIdFilter_ReturnsFilteredResults()
    {
        // Arrange
        var context = CreateContext();
        var orderId = Guid.NewGuid();
        var shipment1 = await SeedShipmentAsync(context, orderId: orderId);
        var shipment2 = await SeedShipmentAsync(context, orderId: Guid.NewGuid());
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);

        // Act
        var result = await controller.GetShipments(orderId: orderId, status: null, carrier: null, from: null, to: null);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(1);
        result.Value!.First().OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task GetShipments_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var context = CreateContext();
        var shipment1 = await SeedShipmentAsync(context, status: ShipmentStatus.PendingDetails);
        var shipment2 = await SeedShipmentAsync(context, status: ShipmentStatus.Scheduled);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);

        // Act
        var result = await controller.GetShipments(orderId: null, status: ShipmentStatus.PendingDetails, carrier: null, from: null, to: null);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(1);
        result.Value!.First().Status.Should().Be(ShipmentStatus.PendingDetails);
    }

    [Fact]
    public async Task GetByOrder_WithValidOrderId_ReturnsShipments()
    {
        // Arrange
        var context = CreateContext();
        var orderId = Guid.NewGuid();
        var shipment = await SeedShipmentAsync(context, orderId: orderId);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);

        // Act
        var result = await controller.GetByOrder(orderId);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(1);
        result.Value!.First().OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task ScheduleShipment_WithValidRequest_SchedulesShipment()
    {
        // Arrange
        var context = CreateContext();
        var shipment = await SeedShipmentAsync(context, status: ShipmentStatus.PendingDetails);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);
        var request = new ScheduleShipmentRequest
        {
            Carrier = "sandbox",
            ServiceLevel = "ground",
            PickupDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await controller.ScheduleShipment(shipment.Id, request);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(ShipmentStatus.Scheduled);
    }

    [Fact]
    public async Task ScheduleShipment_WithCancelledShipment_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateContext();
        var shipment = await SeedShipmentAsync(context, status: ShipmentStatus.Cancelled);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);
        var request = new ScheduleShipmentRequest();

        // Act
        var result = await controller.ScheduleShipment(shipment.Id, request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateStatus_WithValidRequest_UpdatesStatus()
    {
        // Arrange
        var context = CreateContext();
        var shipment = await SeedShipmentAsync(context);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);
        var request = new ShipmentStatusUpdateRequest
        {
            Status = ShipmentStatus.InTransit,
            Notes = "Shipment is in transit"
        };

        // Act
        var result = await controller.UpdateStatus(shipment.Id, request);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(ShipmentStatus.InTransit);
    }

    [Fact]
    public async Task CancelShipment_WithValidRequest_CancelsShipment()
    {
        // Arrange
        var context = CreateContext();
        var shipment = await SeedShipmentAsync(context);
        var fulfillmentService = CreateFulfillmentService(context);
        var logger = CreateLogger();
        var controller = new ShipmentsController(fulfillmentService, logger);
        SetupControllerContext(controller);
        var request = new CancelShipmentRequest
        {
            Reason = "Customer requested cancellation"
        };

        // Act
        var result = await controller.CancelShipment(shipment.Id, request);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    private static FulfillmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new FulfillmentDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static IFulfillmentService CreateFulfillmentService(FulfillmentDbContext context)
    {
        var logger = CreateServiceLogger();
        var options = Options.Create(new CarrierOptions { DefaultCarrier = "sandbox" });
        var carrierLogger = new Mock<ILogger<FakeCarrierProvider>>().Object;
        var carrierProvider = new FakeCarrierProvider(carrierLogger, options);
        
        var publishEndpoint = CreatePublishEndpoint();
        return new FulfillmentService.Services.FulfillmentService(context, carrierProvider, publishEndpoint.Object, logger);
    }

    private static Mock<IPublishEndpoint> CreatePublishEndpoint()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return publishEndpoint;
    }

    private static void SetupControllerContext(ShipmentsController controller)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    private static ILogger<ShipmentsController> CreateLogger()
    {
        return new Mock<ILogger<ShipmentsController>>().Object;
    }

    private static ILogger<FulfillmentService.Services.FulfillmentService> CreateServiceLogger()
    {
        return new Mock<ILogger<FulfillmentService.Services.FulfillmentService>>().Object;
    }

    private static async Task<Shipment> SeedShipmentAsync(
        FulfillmentDbContext context,
        Guid? orderId = null,
        ShipmentStatus status = ShipmentStatus.PendingDetails)
    {
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId ?? Guid.NewGuid(),
            UserId = "user123",
            Status = status,
            DeclaredValue = 99.99m,
            TotalWeight = 0.5m,
            Destination = new ShipmentAddress
            {
                RecipientName = "John Doe",
                Line1 = "123 Main St",
                City = "New York",
                State = "NY",
                PostalCode = "10001",
                Country = "USA"
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        shipment.Items.Add(new ShipmentItem
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            ProductId = Guid.NewGuid(),
            Sku = "SKU001",
            Name = "Test Product",
            Quantity = 1,
            Weight = 0.5m
        });

        shipment.Timeline.Add(new ShipmentTimelineEntry
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Status = status,
            Description = "Shipment created",
            Source = "api",
            CreatedAt = DateTime.UtcNow
        });

        context.Shipments.Add(shipment);
        await context.SaveChangesAsync();
        return shipment;
    }
}
