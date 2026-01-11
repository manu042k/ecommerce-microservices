using System.ComponentModel.DataAnnotations;

namespace OrderService.Dtos;

public class CreateOrderRequest
{
    [Required]
    public List<OrderItemRequest> Items { get; set; } = new();

    public string? ShippingAddress { get; set; }
}

public class OrderItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public int Quantity { get; set; }
}
