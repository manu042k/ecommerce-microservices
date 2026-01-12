namespace BuildingBlocks.Contracts.Fulfillment;

public interface IShipmentCreated
{
    Guid ShipmentId { get; }
    Guid OrderId { get; }
    string Status { get; }
    DateTime CreatedAt { get; }
}

public interface IShipmentScheduled
{
    Guid ShipmentId { get; }
    Guid OrderId { get; }
    string Carrier { get; }
    string ServiceLevel { get; }
    string TrackingNumber { get; }
    DateTime ScheduledAt { get; }
}

public interface IShipmentStatusChanged
{
    Guid ShipmentId { get; }
    Guid OrderId { get; }
    string Status { get; }
    DateTime ChangedAt { get; }
    string? Notes { get; }
}

public interface IShipmentFailed
{
    Guid ShipmentId { get; }
    Guid OrderId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}
