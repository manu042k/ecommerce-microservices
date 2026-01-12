using System.Text.Json;
using BuildingBlocks.Contracts.Payments;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Models;
using PaymentService.Services.Providers;

namespace PaymentService.Services;

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _dbContext;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentDbContext dbContext,
        IPaymentProvider paymentProvider,
        IPublishEndpoint publishEndpoint,
        ILogger<PaymentService> logger)
    {
        _dbContext = dbContext;
        _paymentProvider = paymentProvider;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Currency = NormalizeCurrency(request.Currency),
            Status = PaymentStatus.Pending,
            Provider = _paymentProvider.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.Payments.AddAsync(payment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var providerResult = await _paymentProvider.CreatePaymentIntentAsync(payment.Id, request, cancellationToken);
            ApplyProviderResult(payment, providerResult);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await PublishStatusEventsAsync(payment, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment intent for order {OrderId}", payment.OrderId);
            payment.Status = PaymentStatus.Failed;
            payment.FailureMessage = ex.Message;
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await PublishStatusEventsAsync(payment, cancellationToken);
        }

        return MapPayment(payment);
    }

    public async Task<PaymentDto?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Refunds)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        return payment is null ? null : MapPayment(payment);
    }

    public async Task<IReadOnlyCollection<PaymentDto>> GetPaymentsAsync(PaymentQueryParameters query, CancellationToken cancellationToken = default)
    {
        var paymentsQuery = _dbContext.Payments
            .Include(p => p.Refunds)
            .AsNoTracking()
            .AsQueryable();

        if (query.OrderId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.OrderId == query.OrderId.Value);
        }

        if (query.Status.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Status == query.Status.Value);
        }

        if (query.From.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.CreatedAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.CreatedAt <= query.To.Value);
        }

        var payments = await paymentsQuery
            .OrderByDescending(p => p.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return payments.Select(MapPayment).ToList();
    }

    public async Task<PaymentDto?> ConfirmPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            throw new InvalidOperationException("Payment intent missing provider identifier");
        }

        var result = await _paymentProvider.ConfirmPaymentIntentAsync(payment.ProviderPaymentId, cancellationToken);
        ApplyProviderResult(payment, result);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishStatusEventsAsync(payment, cancellationToken);
        return MapPayment(payment);
    }

    public async Task<RefundDto> RefundAsync(Guid paymentId, RefundRequest request, CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments.Include(p => p.Refunds).FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            throw new InvalidOperationException("Payment not found");
        }

        if (payment.Status is not PaymentStatus.Succeeded and not PaymentStatus.Refunded)
        {
            throw new InvalidOperationException("Only succeeded payments can be refunded");
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            throw new InvalidOperationException("Payment intent missing provider identifier");
        }

        var amount = request.Amount <= 0 ? payment.Amount : decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var providerResult = await _paymentProvider.RefundPaymentAsync(payment.ProviderPaymentId, amount, cancellationToken);

        var refund = new Refund
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Amount = amount,
            ProviderRefundId = providerResult.ProviderRefundId,
            Status = providerResult.Status,
            FailureCode = providerResult.FailureCode,
            FailureMessage = providerResult.FailureMessage,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = providerResult.Status == RefundStatus.Succeeded ? DateTime.UtcNow : null
        };

        payment.Refunds.Add(refund);
        if (providerResult.Status == RefundStatus.Succeeded)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish<IRefundIssued>(new
        {
            PaymentId = payment.Id,
            RefundId = refund.Id,
            Amount = refund.Amount,
            Currency = payment.Currency,
            ProviderRefundId = refund.ProviderRefundId,
            OccurredAt = DateTime.UtcNow
        }, cancellationToken);

        return MapRefund(refund);
    }

    public async Task CaptureAsync(CapturePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);
        if (payment is null)
        {
            throw new InvalidOperationException("Payment not found");
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            var result = await _paymentProvider.ConfirmPaymentIntentAsync(payment.ProviderPaymentId, cancellationToken);
            ApplyProviderResult(payment, result);
        }
        else
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishStatusEventsAsync(payment, cancellationToken);
    }

    public async Task HandleWebhookAsync(string provider, string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var storedEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            EventId = Guid.NewGuid().ToString(),
            Payload = payload,
            HeadersJson = JsonSerializer.Serialize(headers),
            ReceivedAt = DateTime.UtcNow
        };

        PaymentWebhookResult? webhookResult = null;
        if (string.Equals(provider, _paymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            webhookResult = await _paymentProvider.ParseWebhookAsync(payload, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase), cancellationToken);
            if (webhookResult is not null)
            {
                storedEvent.EventId = webhookResult.EventId;
            }
        }

        _dbContext.WebhookEvents.Add(storedEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (webhookResult is null)
        {
            return;
        }

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.ProviderPaymentId == webhookResult.ProviderPaymentId, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("Received webhook for unknown payment intent {ProviderPaymentId}", webhookResult.ProviderPaymentId);
            return;
        }

        payment.Status = webhookResult.Status;
        payment.FailureCode = webhookResult.FailureCode;
        payment.FailureMessage = webhookResult.FailureMessage;
        payment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        storedEvent.ProcessedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishStatusEventsAsync(payment, cancellationToken);
    }

    private void ApplyProviderResult(Payment payment, ProviderPaymentResult result)
    {
        payment.ProviderPaymentId = result.ProviderPaymentId;
        payment.ProviderClientSecret = result.ClientSecret;
        payment.Status = result.Status;
        payment.FailureCode = result.FailureCode;
        payment.FailureMessage = result.FailureMessage;
        payment.UpdatedAt = DateTime.UtcNow;
    }

    private async Task PublishStatusEventsAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.Succeeded)
        {
            await _publishEndpoint.Publish<IPaymentSucceeded>(new
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Provider = payment.Provider,
                ProviderPaymentId = payment.ProviderPaymentId,
                OccurredAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else if (payment.Status == PaymentStatus.Failed)
        {
            await _publishEndpoint.Publish<IPaymentFailed>(new
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Provider = payment.Provider,
                ProviderPaymentId = payment.ProviderPaymentId,
                ErrorCode = payment.FailureCode,
                ErrorMessage = payment.FailureMessage,
                OccurredAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }

    private static PaymentDto MapPayment(Payment payment)
    {
        var refunds = payment.Refunds
            .OrderByDescending(r => r.CreatedAt)
            .Select(MapRefund)
            .ToList();

        return new PaymentDto(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Currency,
            payment.Status,
            payment.Provider,
            payment.ProviderPaymentId,
            payment.ProviderClientSecret,
            payment.FailureCode,
            payment.FailureMessage,
            payment.CreatedAt,
            payment.UpdatedAt,
            refunds);
    }

    private static RefundDto MapRefund(Refund refund) =>
        new(
            refund.Id,
            refund.Amount,
            refund.Status,
            refund.ProviderRefundId,
            refund.CreatedAt,
            refund.CompletedAt);

    private static string NormalizeCurrency(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "usd" : value.ToLowerInvariant();
}
