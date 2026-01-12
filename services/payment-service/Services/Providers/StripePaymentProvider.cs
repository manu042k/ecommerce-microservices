using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentService.Dtos;
using PaymentService.Models;
using Stripe;

namespace PaymentService.Services.Providers;

public class StripePaymentProvider : IPaymentProvider
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(IOptions<StripeOptions> options, ILogger<StripePaymentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "stripe";

    public async Task<ProviderPaymentResult> CreatePaymentIntentAsync(Guid paymentId, CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Stripe API key not configured, simulating payment intent for order {OrderId}", request.OrderId);
            return new ProviderPaymentResult(PaymentStatus.Succeeded, $"sim-{paymentId}", $"sim-client-secret-{paymentId}", null, null);
        }

        StripeConfiguration.ApiKey = _options.ApiKey;
        var service = new PaymentIntentService();
        var options = new PaymentIntentCreateOptions
        {
            Amount = ToMinorUnits(request.Amount),
            Currency = request.Currency.ToLowerInvariant(),
            Description = request.Description,
            PaymentMethod = request.PaymentMethodId,
            Confirm = true,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = request.OrderId.ToString(),
                ["paymentId"] = paymentId.ToString()
            }
        };

        var intent = await service.CreateAsync(options, BuildRequestOptions(), cancellationToken);
        return MapPaymentIntent(intent);
    }

    public async Task<ProviderPaymentResult> ConfirmPaymentIntentAsync(string providerPaymentId, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return new ProviderPaymentResult(PaymentStatus.Succeeded, providerPaymentId, null, null, null);
        }

        StripeConfiguration.ApiKey = _options.ApiKey;
        var service = new PaymentIntentService();
        var intent = await service.ConfirmAsync(providerPaymentId, new PaymentIntentConfirmOptions(), BuildRequestOptions(), cancellationToken);
        return MapPaymentIntent(intent);
    }

    public async Task<ProviderRefundResult> RefundPaymentAsync(string providerPaymentId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return new ProviderRefundResult(RefundStatus.Succeeded, $"sim-refund-{providerPaymentId}", null, null);
        }

        StripeConfiguration.ApiKey = _options.ApiKey;
        var service = new RefundService();
        var options = new RefundCreateOptions
        {
            PaymentIntent = providerPaymentId,
            Amount = ToMinorUnits(amount)
        };

        var refund = await service.CreateAsync(options, BuildRequestOptions(), cancellationToken);
        return MapRefund(refund);
    }

    public Task<PaymentWebhookResult?> ParseWebhookAsync(string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Stripe webhook secret not configured; ignoring webhook notification");
            return Task.FromResult<PaymentWebhookResult?>(null);
        }

        try
        {
            headers.TryGetValue("Stripe-Signature", out var signature);
            var stripeEvent = string.IsNullOrWhiteSpace(_options.WebhookSecret) || string.IsNullOrWhiteSpace(signature)
                ? EventUtility.ParseEvent(payload)
                : EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);

            if (stripeEvent.Data.Object is PaymentIntent intent)
            {
                var result = new PaymentWebhookResult(
                    stripeEvent.Id,
                    MapStatus(intent.Status),
                    intent.Id,
                    intent.LastPaymentError?.Code,
                    intent.LastPaymentError?.Message);
                return Task.FromResult<PaymentWebhookResult?>(result);
            }

            _logger.LogInformation("Unhandled Stripe webhook type {Type}", stripeEvent.Type);
            return Task.FromResult<PaymentWebhookResult?>(null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to parse Stripe webhook");
            return Task.FromResult<PaymentWebhookResult?>(null);
        }
    }

    private RequestOptions? BuildRequestOptions()
    {
        return _options.IsConfigured
            ? new RequestOptions { ApiKey = _options.ApiKey }
            : null;
    }

    private static long ToMinorUnits(decimal amount) => Convert.ToInt64(decimal.Round(amount * 100, MidpointRounding.AwayFromZero));

    private static ProviderPaymentResult MapPaymentIntent(PaymentIntent intent)
    {
        return new ProviderPaymentResult(
            MapStatus(intent.Status),
            intent.Id,
            intent.ClientSecret,
            intent.LastPaymentError?.Code,
            intent.LastPaymentError?.Message);
    }

    private static ProviderRefundResult MapRefund(Stripe.Refund refund)
    {
        var status = refund.Status switch
        {
            "succeeded" => RefundStatus.Succeeded,
            "failed" => RefundStatus.Failed,
            _ => RefundStatus.Pending
        };

        return new ProviderRefundResult(status, refund.Id, refund.FailureReason, refund.Description);
    }

    private static PaymentStatus MapStatus(string? stripeStatus)
    {
        return stripeStatus switch
        {
            "succeeded" => PaymentStatus.Succeeded,
            "requires_action" => PaymentStatus.RequiresAction,
            "processing" => PaymentStatus.Pending,
            "canceled" => PaymentStatus.Cancelled,
            "requires_payment_method" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        };
    }
}
