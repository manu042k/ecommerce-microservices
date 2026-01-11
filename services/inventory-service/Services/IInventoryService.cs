using InventoryService.Dtos;

namespace InventoryService.Services;

public interface IInventoryService
{
    Task<IReadOnlyCollection<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<InventoryItemDto> AdjustInventoryAsync(InventoryAdjustmentRequest request, string requestedBy, CancellationToken cancellationToken = default);

    Task<ReservationResponse> CreateReservationAsync(CreateReservationRequest request, CancellationToken cancellationToken = default);

    Task<bool> ReleaseReservationAsync(Guid reservationId, string reason, CancellationToken cancellationToken = default);

    Task<bool> CommitReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AvailabilityEntryDto>> GetAvailabilityAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken = default);
}
