using InventoryService.Dtos;
using InventoryService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("internal/inventory/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(IInventoryService inventoryService, ILogger<ReservationsController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ReservationResponse>> CreateReservation([FromBody] CreateReservationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var reservation = await _inventoryService.CreateReservationAsync(request, cancellationToken);
            return Created($"internal/inventory/reservations/{reservation.Id}", reservation);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid reservation request for order {OrderId}", request.OrderId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Reservation failed for order {OrderId}", request.OrderId);
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{reservationId:guid}/release")]
    public async Task<IActionResult> ReleaseReservation(Guid reservationId, [FromBody] ReleaseReservationRequest? request, CancellationToken cancellationToken)
    {
        var reason = request?.Reason ?? "manual-release";
        var success = await _inventoryService.ReleaseReservationAsync(reservationId, reason, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{reservationId:guid}/commit")]
    public async Task<IActionResult> CommitReservation(Guid reservationId, CancellationToken cancellationToken)
    {
        var success = await _inventoryService.CommitReservationAsync(reservationId, cancellationToken);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("availability")]
    public async Task<ActionResult<IReadOnlyCollection<AvailabilityEntryDto>>> GetAvailability([FromBody] AvailabilityRequest request, CancellationToken cancellationToken)
    {
        var availability = await _inventoryService.GetAvailabilityAsync(request.ProductIds, cancellationToken);
        return Ok(availability);
    }
}
