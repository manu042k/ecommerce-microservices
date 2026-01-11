namespace OrderService.Contracts;

public interface IOrderCreated
{
    Guid OrderId { get; }
    string UserId { get; }
    decimal TotalAmount { get; }
    DateTime Timestamp { get; }
}
