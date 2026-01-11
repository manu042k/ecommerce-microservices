namespace OrderService.Contracts;

public interface IOrderStatusChanged
{
    Guid OrderId { get; }
    int PreviousStatus { get; }
    int NewStatus { get; }
    DateTime Timestamp { get; }
}
