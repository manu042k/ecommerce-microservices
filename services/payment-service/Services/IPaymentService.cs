using PaymentService.Dtos;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);

    Task<PaymentDto?> GetPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PaymentDto>> GetPaymentsAsync(PaymentQueryParameters query, CancellationToken cancellationToken = default);

    Task<PaymentDto?> ConfirmPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<RefundDto> RefundAsync(Guid paymentId, RefundRequest request, CancellationToken cancellationToken = default);

    Task CaptureAsync(CapturePaymentRequest request, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string provider, string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default);
}
