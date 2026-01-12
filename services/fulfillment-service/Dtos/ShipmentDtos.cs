using System.ComponentModel.DataAnnotations;
using FulfillmentService.Models;

namespace FulfillmentService.Dtos;

public class ShipmentAddressDto
{
    [Required]
    [MaxLength(128)]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Line1 { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Line2 { get; set; }

    [Required]
    [MaxLength(64)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string State { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Phone { get; set; }
}

public class ShipmentItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [MaxLength(128)]
    public string Sku { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;

    [Range(0.01, 1000)]
    public decimal Weight { get; set; } = 0.1m;
}

public class CreateShipmentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [MaxLength(64)]
    public string? UserId { get; set; }

    [Required]
    public ShipmentAddressDto Destination { get; set; } = new();

    [MinLength(1)]
    public List<ShipmentItemRequest> Items { get; set; } = new();

    [Range(0.01, 100000)]
    public decimal DeclaredValue { get; set; }

    [Range(0.01, 1000)]
    public decimal TotalWeight { get; set; }

    [MaxLength(64)]
    public string? CarrierPreference { get; set; }

    [MaxLength(64)]
    public string? ServiceLevel { get; set; }
}

public class ScheduleShipmentRequest
{
    [MaxLength(64)]
    public string Carrier { get; set; } = "sandbox";

    [MaxLength(64)]
    public string ServiceLevel { get; set; } = "ground";

    [Range(0.01, 1000)]
    public decimal? TotalWeight { get; set; }

    public DateTime? PickupDate { get; set; }
}

public class ShipmentStatusUpdateRequest
{
    [Required]
    public ShipmentStatus Status { get; set; }

    [MaxLength(512)]
    public string? Notes { get; set; }
}

public class CancelShipmentRequest
{
    [MaxLength(512)]
    public string? Reason { get; set; }
}

public class ShipmentQueryParameters
{
    public Guid? OrderId { get; set; }

    public ShipmentStatus? Status { get; set; }

    public string? Carrier { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public record ShipmentItemDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    decimal Weight);

public record ShipmentTimelineEntryDto(
    Guid Id,
    ShipmentStatus Status,
    string Description,
    string Source,
    DateTime CreatedAt);

public record ShipmentDto(
    Guid Id,
    Guid OrderId,
    string? UserId,
    ShipmentStatus Status,
    ShipmentAddressDto? Destination,
    decimal DeclaredValue,
    decimal TotalWeight,
    string? Carrier,
    string? ServiceLevel,
    string? TrackingNumber,
    string? LabelUrl,
    DateTime? ScheduledPickupDate,
    DateTime? EstimatedDelivery,
    DateTime? DeliveredAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<ShipmentItemDto> Items,
    IReadOnlyCollection<ShipmentTimelineEntryDto> Timeline);
