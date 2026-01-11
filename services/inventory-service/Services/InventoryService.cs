using InventoryService.Data;
using InventoryService.Dtos;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Services;

public class InventoryService : IInventoryService
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(InventoryDbContext dbContext, ILogger<InventoryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.InventoryItems
            .AsNoTracking()
            .OrderBy(i => i.ProductName)
            .ToListAsync(cancellationToken);

        return items.Select(MapInventory).ToList();
    }

    public async Task<InventoryItemDto> AdjustInventoryAsync(InventoryAdjustmentRequest request, string requestedBy, CancellationToken cancellationToken = default)
    {
        if (request.QuantityDelta == 0 && !request.ReorderPoint.HasValue && !request.SafetyStock.HasValue)
        {
            throw new ArgumentException("Adjustment must change quantity or thresholds", nameof(request));
        }

        var item = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.ProductId == request.ProductId, cancellationToken);

        if (item == null)
        {
            item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                ProductName = request.ProductName,
                Sku = request.Sku,
                ReorderPoint = request.ReorderPoint ?? 0,
                SafetyStock = request.SafetyStock ?? 0,
                QuantityOnHand = 0,
                QuantityReserved = 0
            };

            await _dbContext.InventoryItems.AddAsync(item, cancellationToken);
        }

        if (item.QuantityOnHand + request.QuantityDelta < 0)
        {
            throw new InvalidOperationException($"Cannot reduce {item.ProductId} below zero");
        }

        item.ProductName = request.ProductName;
        if (!string.IsNullOrWhiteSpace(request.Sku))
        {
            item.Sku = request.Sku;
        }

        item.QuantityOnHand += request.QuantityDelta;
        if (request.ReorderPoint.HasValue)
        {
            item.ReorderPoint = request.ReorderPoint.Value;
        }

        if (request.SafetyStock.HasValue)
        {
            item.SafetyStock = request.SafetyStock.Value;
        }

        item.UpdatedAt = DateTime.UtcNow;

        var adjustment = new InventoryAdjustment
        {
            Id = Guid.NewGuid(),
            ProductId = item.ProductId,
            QuantityDelta = request.QuantityDelta,
            Reason = request.Reason,
            CreatedBy = requestedBy,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.InventoryAdjustments.AddAsync(adjustment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Inventory adjusted for product {ProductId} by {Delta} units", item.ProductId, request.QuantityDelta);
        return MapInventory(item);
    }

    public async Task<ReservationResponse> CreateReservationAsync(CreateReservationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new ArgumentException("At least one line item is required", nameof(request));
        }

        var now = DateTime.UtcNow;
        var holdMinutes = Math.Clamp(request.HoldMinutes, 1, 240);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var inventoryItems = await _dbContext.InventoryItems
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        foreach (var line in request.Items)
        {
            if (!inventoryItems.TryGetValue(line.ProductId, out var inventoryItem))
            {
                throw new InvalidOperationException($"Inventory not found for product {line.ProductId}");
            }

            if (line.Quantity <= 0)
            {
                throw new ArgumentException("Quantity must be positive", nameof(request));
            }

            var available = inventoryItem.QuantityOnHand - inventoryItem.QuantityReserved;
            if (available < line.Quantity)
            {
                throw new InvalidOperationException($"Insufficient quantity for product {line.ProductId}. Requested {line.Quantity}, available {available}");
            }

            inventoryItem.QuantityReserved += line.Quantity;
            inventoryItem.UpdatedAt = now;
        }

        var reservation = new InventoryReservation
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Status = InventoryReservationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(holdMinutes)
        };

        foreach (var line in request.Items)
        {
            reservation.Items.Add(new InventoryReservationItem
            {
                Id = Guid.NewGuid(),
                ProductId = line.ProductId,
                Quantity = line.Quantity
            });
        }

        await _dbContext.InventoryReservations.AddAsync(reservation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Reserved inventory for order {OrderId} with reservation {ReservationId}", reservation.OrderId, reservation.Id);
        return MapReservation(reservation);
    }

    public async Task<bool> ReleaseReservationAsync(Guid reservationId, string reason, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var reservation = await _dbContext.InventoryReservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return false;
        }

        if (reservation.Status is InventoryReservationStatus.Released or InventoryReservationStatus.Failed or InventoryReservationStatus.Expired)
        {
            return true;
        }

        if (reservation.Status == InventoryReservationStatus.Confirmed)
        {
            _logger.LogWarning("Attempted to release confirmed reservation {ReservationId}", reservationId);
            return false;
        }

        var productIds = reservation.Items.Select(i => i.ProductId).Distinct().ToList();
        var inventoryItems = await _dbContext.InventoryItems
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        foreach (var item in reservation.Items)
        {
            if (inventoryItems.TryGetValue(item.ProductId, out var inventoryItem))
            {
                inventoryItem.QuantityReserved = Math.Max(0, inventoryItem.QuantityReserved - item.Quantity);
                inventoryItem.UpdatedAt = DateTime.UtcNow;
            }
        }

        reservation.Status = InventoryReservationStatus.Released;
        reservation.CompletedAt = DateTime.UtcNow;
        reservation.FailureReason = reason;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Released reservation {ReservationId}", reservationId);
        return true;
    }

    public async Task<bool> CommitReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var reservation = await _dbContext.InventoryReservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return false;
        }

        if (reservation.Status == InventoryReservationStatus.Confirmed)
        {
            return true;
        }

        if (reservation.Status is InventoryReservationStatus.Released or InventoryReservationStatus.Failed or InventoryReservationStatus.Expired)
        {
            _logger.LogWarning("Cannot commit reservation {ReservationId} in status {Status}", reservationId, reservation.Status);
            return false;
        }

        var productIds = reservation.Items.Select(i => i.ProductId).Distinct().ToList();
        var inventoryItems = await _dbContext.InventoryItems
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        foreach (var item in reservation.Items)
        {
            if (!inventoryItems.TryGetValue(item.ProductId, out var inventoryItem))
            {
                throw new InvalidOperationException($"Inventory missing for product {item.ProductId}");
            }

            if (inventoryItem.QuantityReserved < item.Quantity)
            {
                throw new InvalidOperationException($"Reservation data inconsistent for product {item.ProductId}");
            }

            if (inventoryItem.QuantityOnHand < item.Quantity)
            {
                throw new InvalidOperationException($"Insufficient on-hand quantity for product {item.ProductId}");
            }

            inventoryItem.QuantityReserved -= item.Quantity;
            inventoryItem.QuantityOnHand -= item.Quantity;
            inventoryItem.UpdatedAt = DateTime.UtcNow;
        }

        reservation.Status = InventoryReservationStatus.Confirmed;
        reservation.CompletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Committed reservation {ReservationId}", reservationId);
        return true;
    }

    public async Task<IReadOnlyCollection<AvailabilityEntryDto>> GetAvailabilityAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<AvailabilityEntryDto>();
        }

        var items = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .ToListAsync(cancellationToken);

        return items
            .Select(i => new AvailabilityEntryDto(i.ProductId, i.QuantityOnHand, i.QuantityReserved, i.QuantityOnHand - i.QuantityReserved))
            .ToList();
    }

    private static InventoryItemDto MapInventory(InventoryItem item) =>
        new(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.Sku,
            item.QuantityOnHand,
            item.QuantityReserved,
            item.QuantityOnHand - item.QuantityReserved,
            item.ReorderPoint,
            item.SafetyStock,
            item.UpdatedAt);

    private static ReservationResponse MapReservation(InventoryReservation reservation)
    {
        var lines = reservation.Items
            .Select(i => new ReservationLineResponse(i.ProductId, i.Quantity))
            .ToList();

        return new ReservationResponse(
            reservation.Id,
            reservation.OrderId,
            reservation.Status,
            reservation.CreatedAt,
            reservation.ExpiresAt,
            reservation.CompletedAt,
            reservation.FailureReason,
            lines);
    }
}
