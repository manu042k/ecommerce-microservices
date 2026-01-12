using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentService.Models;

public enum ShipmentStatus
{
    PendingDetails = 0,
    ReadyToSchedule = 1,
    Scheduled = 2,
    LabelGenerated = 3,
    InTransit = 4,
    Delivered = 5,
    Cancelled = 6,
    Failed = 7
}

public class Shipment
{
    public Guid Id { get; set; }

    [Required]
    public Guid OrderId { get; set; }

    [MaxLength(64)]
    public string? UserId { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.PendingDetails;

    [Precision(18, 2)]
    public decimal DeclaredValue { get; set; }

    [Precision(18, 2)]
    public decimal TotalWeight { get; set; }

    public ShipmentAddress? Destination { get; set; }

    [MaxLength(64)]
    public string? Carrier { get; set; }

    [MaxLength(64)]
    public string? ServiceLevel { get; set; }

    [MaxLength(256)]
    public string? TrackingNumber { get; set; }

    [MaxLength(512)]
    public string? LabelUrl { get; set; }

    public DateTime? ScheduledPickupDate { get; set; }

    public DateTime? EstimatedDelivery { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();

    public ICollection<ShipmentTimelineEntry> Timeline { get; set; } = new List<ShipmentTimelineEntry>();
}

[Owned]
public class ShipmentAddress
{
    [MaxLength(128)]
    public string RecipientName { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Line1 { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Line2 { get; set; }

    [MaxLength(64)]
    public string City { get; set; } = string.Empty;

    [MaxLength(64)]
    public string State { get; set; } = string.Empty;

    [MaxLength(32)]
    public string PostalCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Phone { get; set; }
}

public class ShipmentItem
{
    public Guid Id { get; set; }

    public Guid ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    [Required]
    public Guid ProductId { get; set; }

    [MaxLength(128)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    [Precision(18, 3)]
    public decimal Weight { get; set; }
}

public class ShipmentTimelineEntry
{
    public Guid Id { get; set; }

    public Guid ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public ShipmentStatus Status { get; set; }

    [MaxLength(512)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Source { get; set; } = "system";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
