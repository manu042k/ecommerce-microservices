using System;

namespace BuildingBlocks.Contracts.Payments;

public interface IPaymentSucceeded
{
    Guid PaymentId { get; }
    Guid OrderId { get; }
    decimal Amount { get; }
    string Currency { get; }
    string Provider { get; }
    string ProviderPaymentId { get; }
    DateTime OccurredAt { get; }
}

public interface IPaymentFailed
{
    Guid PaymentId { get; }
    Guid OrderId { get; }
    string Provider { get; }
    string? ProviderPaymentId { get; }
    string? ErrorCode { get; }
    string? ErrorMessage { get; }
    DateTime OccurredAt { get; }
}

public interface IRefundIssued
{
    Guid PaymentId { get; }
    Guid RefundId { get; }
    decimal Amount { get; }
    string Currency { get; }
    string ProviderRefundId { get; }
    DateTime OccurredAt { get; }
}
