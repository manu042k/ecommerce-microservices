using OrderService.Dtos;
using OrderService.Models;

namespace OrderService.Services;

public interface IOrderService
{
    Task<OrderResponse?> GetOrderByIdAsync(Guid orderId);
    Task<List<OrderResponse>> GetUserOrdersAsync(string userId);
    Task<OrderResponse> CreateOrderAsync(string userId, CreateOrderRequest request);
    Task<OrderResponse?> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus);
    Task<List<OrderResponse>> GetOrdersByStatusAsync(OrderStatus status);
    Task<bool> DeleteOrderAsync(Guid orderId);
}
