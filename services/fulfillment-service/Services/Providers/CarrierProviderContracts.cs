namespace FulfillmentService.Services.Providers;

public class CarrierOptions
{
    public string DefaultCarrier { get; set; } = "sandbox";

    public int TrackingCacheMinutes { get; set; } = 15;
}

public record CarrierBookingResult(
    bool Success,
    string Carrier,
    string ServiceLevel,
    string TrackingNumber,
    string LabelUrl,
    DateTime? EstimatedDelivery,
    string? FailureReason = null);

public record TrackingUpdate(
    bool Success,
    string TrackingNumber,
    string Status,
    DateTime OccurredAt,
    string? Notes = null);

public interface ICarrierProvider
{
    string Name { get; }

    Task<CarrierBookingResult> BookShipmentAsync(CarrierBookingContext context, CancellationToken cancellationToken = default);

    Task<TrackingUpdate?> GetLatestTrackingAsync(string trackingNumber, CancellationToken cancellationToken = default);
}

public record CarrierBookingContext(
    Guid ShipmentId,
    Guid OrderId,
    decimal TotalWeight,
    decimal DeclaredValue,
    string DestinationCountry,
    string DestinationPostalCode,
    DateTime? RequestedPickupDate,
    string? ServiceLevel,
    IReadOnlyCollection<CarrierBookingItem> Items);

public record CarrierBookingItem(
    Guid ProductId,
    string Sku,
    int Quantity,
    decimal Weight);
