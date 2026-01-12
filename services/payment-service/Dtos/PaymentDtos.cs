using System.ComponentModel.DataAnnotations;
using PaymentService.Models;

namespace PaymentService.Dtos;

public class CreatePaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Range(0.5, 100000)]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(8)]
    public string Currency { get; set; } = "usd";

    [Required]
    [MaxLength(256)]
    public string PaymentMethodId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }
}

public record PaymentDto(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Provider,
    string ProviderPaymentId,
    string? ClientSecret,
    string? FailureCode,
    string? FailureMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<RefundDto> Refunds);

public class PaymentQueryParameters
{
    public Guid? OrderId { get; set; }

    public PaymentStatus? Status { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public class RefundRequest
{
    [Range(0.5, 100000)]
    public decimal Amount { get; set; }

    [MaxLength(256)]
    public string Reason { get; set; } = "customer-request";
}

public record RefundDto(
    Guid Id,
    decimal Amount,
    RefundStatus Status,
    string ProviderRefundId,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public class CapturePaymentRequest
{
    [Required]
    public Guid PaymentId { get; set; }

    [Range(0.5, 100000)]
    public decimal Amount { get; set; }
}
