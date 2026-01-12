using FulfillmentService.Dtos;
using FulfillmentService.Models;

namespace FulfillmentService.Services;

public interface IFulfillmentService
{
    Task<ShipmentDto> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken = default);

    Task<ShipmentDto?> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ShipmentDto>> GetShipmentsAsync(ShipmentQueryParameters query, CancellationToken cancellationToken = default);

    Task<ShipmentDto?> ScheduleShipmentAsync(Guid shipmentId, ScheduleShipmentRequest request, CancellationToken cancellationToken = default);

    Task<ShipmentDto?> UpdateStatusAsync(Guid shipmentId, ShipmentStatus status, string? notes, string source, CancellationToken cancellationToken = default);

    Task<ShipmentDto?> CancelShipmentAsync(Guid shipmentId, string reason, CancellationToken cancellationToken = default);

    Task HandlePaymentSucceededAsync(Guid orderId, Guid paymentId, decimal amount, string currency, CancellationToken cancellationToken = default);

    Task HandlePaymentFailedAsync(Guid orderId, Guid paymentId, string? errorCode, string? errorMessage, CancellationToken cancellationToken = default);
}
