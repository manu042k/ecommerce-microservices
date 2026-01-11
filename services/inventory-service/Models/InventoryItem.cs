using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryService.Models;

public enum InventoryReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Released = 2,
    Failed = 3,
    Expired = 4
}

public class InventoryItem
{
    public Guid Id { get; set; }

    [Required]
    public Guid ProductId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public int ReorderPoint { get; set; } = 0;

    public int SafetyStock { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public int AvailableQuantity => QuantityOnHand - QuantityReserved;
}

public class InventoryReservation
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public InventoryReservationStatus Status { get; set; } = InventoryReservationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? FailureReason { get; set; }

    public List<InventoryReservationItem> Items { get; set; } = new();
}

public class InventoryReservationItem
{
    public Guid Id { get; set; }

    public Guid ReservationId { get; set; }

    public InventoryReservation? Reservation { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}

public class InventoryAdjustment
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public int QuantityDelta { get; set; }

    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(128)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
