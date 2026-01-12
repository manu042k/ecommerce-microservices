using BuildingBlocks.Contracts.Fulfillment;
using FulfillmentService.Data;
using FulfillmentService.Dtos;
using FulfillmentService.Models;
using FulfillmentService.Services.Providers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FulfillmentService.Services;

public class FulfillmentService : IFulfillmentService
{
    private const string TimelineSourceApi = "api";
    private const string TimelineSourceSystem = "system";

    private readonly FulfillmentDbContext _dbContext;
    private readonly ICarrierProvider _carrierProvider;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FulfillmentService> _logger;

    public FulfillmentService(
        FulfillmentDbContext dbContext,
        ICarrierProvider carrierProvider,
        IPublishEndpoint publishEndpoint,
        ILogger<FulfillmentService> logger)
    {
        _dbContext = dbContext;
        _carrierProvider = carrierProvider;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<ShipmentDto> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Destination is null)
        {
            throw new ArgumentException("Destination is required", nameof(request.Destination));
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ArgumentException("At least one shipment item is required", nameof(request.Items));
        }

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            UserId = request.UserId,
            Status = ShipmentStatus.PendingDetails,
            Destination = MapAddress(request.Destination),
            DeclaredValue = decimal.Round(request.DeclaredValue, 2, MidpointRounding.AwayFromZero),
            TotalWeight = decimal.Round(request.TotalWeight, 2, MidpointRounding.AwayFromZero),
            Carrier = request.CarrierPreference,
            ServiceLevel = request.ServiceLevel ?? "ground",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        shipment.Timeline.Add(new ShipmentTimelineEntry
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.PendingDetails,
            Description = "Shipment created",
            Source = TimelineSourceApi,
            CreatedAt = DateTime.UtcNow
        });

        shipment.Items = request.Items.Select(item => new ShipmentItem
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            ProductId = item.ProductId,
            Sku = item.Sku,
            Name = item.Name,
            Quantity = item.Quantity,
            Weight = decimal.Round(item.Weight, 3, MidpointRounding.AwayFromZero)
        }).ToList();

        await _dbContext.Shipments.AddAsync(shipment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishShipmentCreatedAsync(shipment, cancellationToken);
        return MapShipmentDto(shipment);
    }

    public async Task<ShipmentDto?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await LoadShipment(shipmentId, cancellationToken);
        return shipment is null ? null : MapShipmentDto(shipment);
    }

    public async Task<IReadOnlyCollection<ShipmentDto>> GetShipmentsAsync(ShipmentQueryParameters query, CancellationToken cancellationToken = default)
    {
        var shipmentsQuery = _dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Timeline)
            .AsNoTracking()
            .AsQueryable();

        if (query.OrderId.HasValue)
        {
            shipmentsQuery = shipmentsQuery.Where(s => s.OrderId == query.OrderId.Value);
        }

        if (query.Status.HasValue)
        {
            shipmentsQuery = shipmentsQuery.Where(s => s.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Carrier))
        {
            var normalizedCarrier = query.Carrier.Trim().ToLowerInvariant();
            shipmentsQuery = shipmentsQuery.Where(s => s.Carrier == normalizedCarrier);
        }

        if (query.From.HasValue)
        {
            shipmentsQuery = shipmentsQuery.Where(s => s.CreatedAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            shipmentsQuery = shipmentsQuery.Where(s => s.CreatedAt <= query.To.Value);
        }

        var shipments = await shipmentsQuery
            .OrderByDescending(s => s.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return shipments.Select(MapShipmentDto).ToList();
    }

    public async Task<ShipmentDto?> ScheduleShipmentAsync(Guid shipmentId, ScheduleShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Timeline)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

        if (shipment is null)
        {
            return null;
        }

        if (shipment.Status is ShipmentStatus.Cancelled or ShipmentStatus.Failed)
        {
            throw new InvalidOperationException("Cannot schedule shipment that is cancelled or failed");
        }

        if (shipment.Destination is null)
        {
            throw new InvalidOperationException("Shipment is missing destination details");
        }

        var bookingContext = new CarrierBookingContext(
            shipment.Id,
            shipment.OrderId,
            request.TotalWeight ?? shipment.TotalWeight,
            shipment.DeclaredValue,
            shipment.Destination.Country,
            shipment.Destination.PostalCode,
            request.PickupDate,
            request.ServiceLevel ?? shipment.ServiceLevel,
            shipment.Items.Select(i => new CarrierBookingItem(i.ProductId, i.Sku, i.Quantity, i.Weight)).ToList());

        var result = await _carrierProvider.BookShipmentAsync(bookingContext, cancellationToken);
        if (!result.Success)
        {
            shipment.Status = ShipmentStatus.Failed;
            shipment.UpdatedAt = DateTime.UtcNow;
            AddTimelineEntry(shipment, ShipmentStatus.Failed, result.FailureReason ?? "Carrier booking failed", TimelineSourceSystem);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await PublishShipmentFailedAsync(shipment, result.FailureReason, cancellationToken);
            throw new InvalidOperationException(result.FailureReason ?? "Carrier booking failed");
        }

        shipment.Status = ShipmentStatus.Scheduled;
        shipment.Carrier = result.Carrier;
        shipment.ServiceLevel = result.ServiceLevel;
        shipment.TrackingNumber = result.TrackingNumber;
        shipment.LabelUrl = result.LabelUrl;
        shipment.EstimatedDelivery = result.EstimatedDelivery;
        shipment.ScheduledPickupDate = request.PickupDate ?? DateTime.UtcNow.AddDays(1);
        shipment.TotalWeight = bookingContext.TotalWeight;
        shipment.UpdatedAt = DateTime.UtcNow;

        AddTimelineEntry(shipment, ShipmentStatus.Scheduled, "Shipment scheduled with carrier", TimelineSourceSystem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish<IShipmentScheduled>(new
        {
            ShipmentId = shipment.Id,
            OrderId = shipment.OrderId,
            Carrier = shipment.Carrier,
            ServiceLevel = shipment.ServiceLevel,
            TrackingNumber = shipment.TrackingNumber,
            ScheduledAt = DateTime.UtcNow
        }, cancellationToken);

        return MapShipmentDto(shipment);
    }

    public async Task<ShipmentDto?> UpdateStatusAsync(Guid shipmentId, ShipmentStatus status, string? notes, string source, CancellationToken cancellationToken = default)
    {
        var shipment = await _dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Timeline)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

        if (shipment is null)
        {
            return null;
        }

        shipment.Status = status;
        shipment.UpdatedAt = DateTime.UtcNow;

        if (status == ShipmentStatus.Delivered)
        {
            shipment.DeliveredAt = DateTime.UtcNow;
        }

        AddTimelineEntry(shipment, status, notes ?? $"Status updated to {status}", source);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishShipmentStatusChangedAsync(shipment, notes, cancellationToken);
        return MapShipmentDto(shipment);
    }

    public async Task<ShipmentDto?> CancelShipmentAsync(Guid shipmentId, string reason, CancellationToken cancellationToken = default)
    {
        var shipment = await _dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Timeline)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);

        if (shipment is null)
        {
            return null;
        }

        shipment.Status = ShipmentStatus.Cancelled;
        shipment.UpdatedAt = DateTime.UtcNow;

        var details = string.IsNullOrWhiteSpace(reason) ? "Cancelled via API" : reason;
        AddTimelineEntry(shipment, ShipmentStatus.Cancelled, details, TimelineSourceApi);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishShipmentStatusChangedAsync(shipment, details, cancellationToken);
        return MapShipmentDto(shipment);
    }

    public async Task HandlePaymentSucceededAsync(Guid orderId, Guid paymentId, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received payment success for order {OrderId} via payment {PaymentId}", orderId, paymentId);
        var existingShipment = await _dbContext.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);
        if (existingShipment is not null)
        {
            return;
        }

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            DeclaredValue = amount,
            Status = ShipmentStatus.PendingDetails,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        shipment.Timeline.Add(new ShipmentTimelineEntry
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.PendingDetails,
            Description = $"Payment received ({currency})",
            Source = TimelineSourceSystem,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.Shipments.AddAsync(shipment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishShipmentCreatedAsync(shipment, cancellationToken);
    }

    public async Task HandlePaymentFailedAsync(Guid orderId, Guid paymentId, string? errorCode, string? errorMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Payment failure for order {OrderId} (payment {PaymentId})", orderId, paymentId);
        var shipments = await _dbContext.Shipments
            .Include(s => s.Timeline)
            .Where(s => s.OrderId == orderId && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Delivered)
            .ToListAsync(cancellationToken);

        foreach (var shipment in shipments)
        {
            shipment.Status = ShipmentStatus.Cancelled;
            shipment.UpdatedAt = DateTime.UtcNow;
            AddTimelineEntry(shipment, ShipmentStatus.Cancelled, errorMessage ?? "Payment failed", TimelineSourceSystem);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var shipment in shipments)
        {
            await PublishShipmentStatusChangedAsync(shipment, errorMessage, cancellationToken);
        }
    }

    private static ShipmentAddress MapAddress(ShipmentAddressDto dto) => new()
    {
        RecipientName = dto.RecipientName,
        Line1 = dto.Line1,
        Line2 = dto.Line2,
        City = dto.City,
        State = dto.State,
        PostalCode = dto.PostalCode,
        Country = dto.Country,
        Phone = dto.Phone
    };

    private async Task<Shipment?> LoadShipment(Guid shipmentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Timeline)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);
    }

    private static ShipmentDto MapShipmentDto(Shipment shipment)
    {
        ShipmentAddressDto? address = null;
        if (shipment.Destination is not null)
        {
            address = new ShipmentAddressDto
            {
                RecipientName = shipment.Destination.RecipientName,
                Line1 = shipment.Destination.Line1,
                Line2 = shipment.Destination.Line2,
                City = shipment.Destination.City,
                State = shipment.Destination.State,
                PostalCode = shipment.Destination.PostalCode,
                Country = shipment.Destination.Country,
                Phone = shipment.Destination.Phone
            };
        }

        var items = shipment.Items
            .OrderBy(i => i.Name)
            .Select(i => new ShipmentItemDto(i.Id, i.ProductId, i.Sku, i.Name, i.Quantity, i.Weight))
            .ToList();

        var timeline = shipment.Timeline
            .OrderBy(t => t.CreatedAt)
            .Select(t => new ShipmentTimelineEntryDto(t.Id, t.Status, t.Description, t.Source, t.CreatedAt))
            .ToList();

        return new ShipmentDto(
            shipment.Id,
            shipment.OrderId,
            shipment.UserId,
            shipment.Status,
            address,
            shipment.DeclaredValue,
            shipment.TotalWeight,
            shipment.Carrier,
            shipment.ServiceLevel,
            shipment.TrackingNumber,
            shipment.LabelUrl,
            shipment.ScheduledPickupDate,
            shipment.EstimatedDelivery,
            shipment.DeliveredAt,
            shipment.CreatedAt,
            shipment.UpdatedAt,
            items,
            timeline);
    }

    private void AddTimelineEntry(Shipment shipment, ShipmentStatus status, string description, string source)
    {
        var timelineEntry = new ShipmentTimelineEntry
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipment.Id,
            Status = status,
            Description = description,
            Source = source,
            CreatedAt = DateTime.UtcNow
        };

        shipment.Timeline.Add(timelineEntry);

        // When the shipment is already tracked, explicitly mark the entry as Added so EF issues an INSERT.
        if (_dbContext.Entry(shipment).State != EntityState.Detached)
        {
            _dbContext.Entry(timelineEntry).State = EntityState.Added;
        }
    }

    private async Task PublishShipmentCreatedAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish<IShipmentCreated>(new
        {
            ShipmentId = shipment.Id,
            OrderId = shipment.OrderId,
            Status = shipment.Status.ToString(),
            CreatedAt = shipment.CreatedAt
        }, cancellationToken);
    }

    private async Task PublishShipmentStatusChangedAsync(Shipment shipment, string? notes, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish<IShipmentStatusChanged>(new
        {
            ShipmentId = shipment.Id,
            OrderId = shipment.OrderId,
            Status = shipment.Status.ToString(),
            ChangedAt = DateTime.UtcNow,
            Notes = notes
        }, cancellationToken);
    }

    private async Task PublishShipmentFailedAsync(Shipment shipment, string? reason, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish<IShipmentFailed>(new
        {
            ShipmentId = shipment.Id,
            OrderId = shipment.OrderId,
            Reason = reason ?? "Unknown",
            FailedAt = DateTime.UtcNow
        }, cancellationToken);
    }
}
