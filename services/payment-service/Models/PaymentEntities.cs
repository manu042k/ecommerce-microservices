using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PaymentService.Models;

public enum PaymentStatus
{
    Pending = 0,
    RequiresAction = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}

public enum RefundStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

public class Payment
{
    public Guid Id { get; set; }

    [Required]
    public Guid OrderId { get; set; }

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "usd";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(64)]
    public string Provider { get; set; } = "stripe";

    [MaxLength(128)]
    public string ProviderPaymentId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ProviderClientSecret { get; set; }

    [MaxLength(64)]
    public string? FailureCode { get; set; }

    [MaxLength(512)]
    public string? FailureMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}

public class Refund
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    [Precision(18, 2)]
    public decimal Amount { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    [MaxLength(128)]
    public string ProviderRefundId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? FailureCode { get; set; }

    [MaxLength(512)]
    public string? FailureMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}

public class WebhookEvent
{
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string Provider { get; set; } = string.Empty;

    [MaxLength(256)]
    public string EventId { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string HeadersJson { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}
