using PaymentService.Dtos;
using PaymentService.Models;

namespace PaymentService.Services.Providers;

public interface IPaymentProvider
{
    string Name { get; }

    Task<ProviderPaymentResult> CreatePaymentIntentAsync(Guid paymentId, CreatePaymentRequest request, CancellationToken cancellationToken = default);

    Task<ProviderPaymentResult> ConfirmPaymentIntentAsync(string providerPaymentId, CancellationToken cancellationToken = default);

    Task<ProviderRefundResult> RefundPaymentAsync(string providerPaymentId, decimal amount, CancellationToken cancellationToken = default);

    Task<PaymentWebhookResult?> ParseWebhookAsync(string payload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}

public record ProviderPaymentResult(
    PaymentStatus Status,
    string ProviderPaymentId,
    string? ClientSecret,
    string? FailureCode,
    string? FailureMessage);

public record ProviderRefundResult(
    RefundStatus Status,
    string ProviderRefundId,
    string? FailureCode,
    string? FailureMessage);

public record PaymentWebhookResult(
    string EventId,
    PaymentStatus Status,
    string ProviderPaymentId,
    string? FailureCode,
    string? FailureMessage);

public class StripeOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
