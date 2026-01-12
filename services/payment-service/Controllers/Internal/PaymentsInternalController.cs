using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Dtos;
using PaymentService.Services;

namespace PaymentService.Controllers.Internal;

[ApiController]
[Route("internal/payments")]
[Authorize]
public class PaymentsInternalController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsInternalController> _logger;

    public PaymentsInternalController(IPaymentService paymentService, ILogger<PaymentsInternalController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("capture")]
    public async Task<IActionResult> Capture([FromBody] CapturePaymentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _paymentService.CaptureAsync(request, cancellationToken);
            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Capture failed for payment {PaymentId}", request.PaymentId);
            return NotFound(ex.Message);
        }
    }
}
