using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;

namespace PaymentService.Controllers.Webhooks;

[ApiController]
[Route("api/payments/webhooks")]
public class PaymentWebhooksController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentWebhooksController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(string provider, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await _paymentService.HandleWebhookAsync(provider, payload, headers, cancellationToken);
        return Ok();
    }
}
