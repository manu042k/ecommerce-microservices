using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FulfillmentService.Services.Providers;

public class FakeCarrierProvider : ICarrierProvider
{
    private readonly ILogger<FakeCarrierProvider> _logger;
    private readonly CarrierOptions _options;

    public FakeCarrierProvider(ILogger<FakeCarrierProvider> logger, IOptions<CarrierOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public string Name => _options.DefaultCarrier;

    public Task<CarrierBookingResult> BookShipmentAsync(CarrierBookingContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Booking sandbox shipment for {ShipmentId} ({Weight} kg)", context.ShipmentId, context.TotalWeight);

        var trackingNumber = $"SANDBOX-{context.ShipmentId.ToString("N")[..8].ToUpperInvariant()}";
        var labelUrl = $"https://sandbox-carrier.local/labels/{context.ShipmentId}";

        return Task.FromResult(new CarrierBookingResult(
            Success: true,
            Carrier: Name,
            ServiceLevel: context.ServiceLevel ?? "ground",
            TrackingNumber: trackingNumber,
            LabelUrl: labelUrl,
            EstimatedDelivery: DateTime.UtcNow.AddDays(5)));
    }

    public Task<TrackingUpdate?> GetLatestTrackingAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var update = new TrackingUpdate(
            Success: true,
            TrackingNumber: trackingNumber,
            Status: "InTransit",
            OccurredAt: DateTime.UtcNow,
            Notes: "Sandbox carrier heartbeat");

        return Task.FromResult<TrackingUpdate?>(update);
    }
}
