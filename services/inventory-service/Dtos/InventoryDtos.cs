using System.ComponentModel.DataAnnotations;
using InventoryService.Models;

namespace InventoryService.Dtos;

public record InventoryItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity,
    int ReorderPoint,
    int SafetyStock,
    DateTime UpdatedAt);

public class InventoryAdjustmentRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Range(-1_000_000, 1_000_000)]
    public int QuantityDelta { get; set; }

    public int? ReorderPoint { get; set; }

    public int? SafetyStock { get; set; }

    [MaxLength(200)]
    public string Reason { get; set; } = "manual-adjustment";
}

public class CreateReservationRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [MinLength(1)]
    public List<ReservationItemRequest> Items { get; set; } = new();

    [Range(1, 240)]
    public int HoldMinutes { get; set; } = 15;
}

public class ReservationItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 10000)]
    public int Quantity { get; set; }
}

public class ReleaseReservationRequest
{
    [MaxLength(200)]
    public string Reason { get; set; } = "manual-release";
}

public record ReservationResponse(
    Guid Id,
    Guid OrderId,
    InventoryReservationStatus Status,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? CompletedAt,
    string? FailureReason,
    IReadOnlyCollection<ReservationLineResponse> Items);

public record ReservationLineResponse(Guid ProductId, int Quantity);

public class AvailabilityRequest
{
    [MinLength(1)]
    public List<Guid> ProductIds { get; set; } = new();
}

public record AvailabilityEntryDto(Guid ProductId, int QuantityOnHand, int QuantityReserved, int AvailableQuantity);
