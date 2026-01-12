using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Dtos;
using PaymentService.Models;
using PaymentService.Services;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = "CustomersOrAdmin")]
    public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.CreatePaymentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPaymentById), new { paymentId = payment.Id }, payment);
    }

    [HttpGet("{paymentId:guid}")]
    [Authorize]
    public async Task<ActionResult<PaymentDto>> GetPaymentById(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetPaymentAsync(paymentId, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpGet]
    [Authorize(Policy = "FinanceOrAdmin")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentDto>>> GetPayments([FromQuery] Guid? orderId, [FromQuery] PaymentStatus? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var query = new PaymentQueryParameters
        {
            OrderId = orderId,
            Status = status,
            From = from,
            To = to
        };

        var payments = await _paymentService.GetPaymentsAsync(query, cancellationToken);
        return Ok(payments);
    }

    [HttpPost("{paymentId:guid}/confirm")]
    [Authorize(Policy = "CustomersOrAdmin")]
    public async Task<ActionResult<PaymentDto>> ConfirmPayment(Guid paymentId, CancellationToken cancellationToken)
    {
        try
        {
            var payment = await _paymentService.ConfirmPaymentAsync(paymentId, cancellationToken);
            return payment is null ? NotFound() : Ok(payment);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Payment confirmation failed for {PaymentId}", paymentId);
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{paymentId:guid}/refund")]
    [Authorize(Policy = "FinanceOrAdmin")]
    public async Task<ActionResult<RefundDto>> Refund(Guid paymentId, [FromBody] RefundRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var refund = await _paymentService.RefundAsync(paymentId, request, cancellationToken);
            return Ok(refund);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Refund failed for {PaymentId}", paymentId);
            return BadRequest(ex.Message);
        }
    }
}
