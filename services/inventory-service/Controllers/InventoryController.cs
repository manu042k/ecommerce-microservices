using System.Security.Claims;
using InventoryService.Dtos;
using InventoryService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "OpsOrAdmin")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<InventoryItemDto>>> GetInventory(CancellationToken cancellationToken)
    {
        var inventory = await _inventoryService.GetInventoryAsync(cancellationToken);
        return Ok(inventory);
    }

    [HttpPost("adjustments")]
    public async Task<ActionResult<InventoryItemDto>> AdjustInventory([FromBody] InventoryAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name
            ?? "system";

        try
        {
            var item = await _inventoryService.AdjustInventoryAsync(request, userId, cancellationToken);
            return Ok(item);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid adjustment request for product {ProductId}", request.ProductId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed adjustment for product {ProductId}", request.ProductId);
            return BadRequest(ex.Message);
        }
    }
}
