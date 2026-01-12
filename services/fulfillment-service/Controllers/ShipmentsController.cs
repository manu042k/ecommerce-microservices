using FulfillmentService.Dtos;
using FulfillmentService.Models;
using FulfillmentService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FulfillmentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShipmentsController : ControllerBase
{
    private readonly IFulfillmentService _fulfillmentService;
    private readonly ILogger<ShipmentsController> _logger;

    public ShipmentsController(IFulfillmentService fulfillmentService, ILogger<ShipmentsController> logger)
    {
        _fulfillmentService = fulfillmentService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "FulfillmentRead")]
    public async Task<ActionResult<IReadOnlyCollection<ShipmentDto>>> GetShipments(
        [FromQuery] Guid? orderId,
        [FromQuery] ShipmentStatus? status,
        [FromQuery] string? carrier,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = new ShipmentQueryParameters
        {
            OrderId = orderId,
            Status = status,
            Carrier = carrier,
            From = from,
            To = to
        };

        var shipments = await _fulfillmentService.GetShipmentsAsync(query, cancellationToken);
        return Ok(shipments);
    }

    [HttpGet("{shipmentId:guid}")]
    [Authorize(Policy = "FulfillmentRead")]
    public async Task<ActionResult<ShipmentDto>> GetShipment(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _fulfillmentService.GetShipmentAsync(shipmentId, cancellationToken);
        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpGet("orders/{orderId:guid}")]
    [Authorize(Policy = "FulfillmentRead")]
    public async Task<ActionResult<IReadOnlyCollection<ShipmentDto>>> GetByOrder(Guid orderId, CancellationToken cancellationToken = default)
    {
        var query = new ShipmentQueryParameters { OrderId = orderId };
        var shipments = await _fulfillmentService.GetShipmentsAsync(query, cancellationToken);
        return Ok(shipments);
    }

    [HttpPost]
    [Authorize(Policy = "FulfillmentWrite")]
    public async Task<ActionResult<ShipmentDto>> CreateShipment([FromBody] CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _fulfillmentService.CreateShipmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetShipment), new { shipmentId = shipment.Id }, shipment);
    }

    [HttpPost("{shipmentId:guid}/schedule")]
    [Authorize(Policy = "FulfillmentWrite")]
    public async Task<ActionResult<ShipmentDto>> ScheduleShipment(Guid shipmentId, [FromBody] ScheduleShipmentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var shipment = await _fulfillmentService.ScheduleShipmentAsync(shipmentId, request, cancellationToken);
            return shipment is null ? NotFound() : Ok(shipment);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to schedule shipment {ShipmentId}", shipmentId);
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{shipmentId:guid}/status")]
    [Authorize(Policy = "FulfillmentWrite")]
    public async Task<ActionResult<ShipmentDto>> UpdateStatus(Guid shipmentId, [FromBody] ShipmentStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _fulfillmentService.UpdateStatusAsync(shipmentId, request.Status, request.Notes, "api", cancellationToken);
        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpPost("{shipmentId:guid}/cancel")]
    [Authorize(Policy = "FulfillmentWrite")]
    public async Task<ActionResult<ShipmentDto>> CancelShipment(Guid shipmentId, [FromBody] CancelShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await _fulfillmentService.CancelShipmentAsync(shipmentId, request.Reason ?? string.Empty, cancellationToken);
        return shipment is null ? NotFound() : Ok(shipment);
    }
}
