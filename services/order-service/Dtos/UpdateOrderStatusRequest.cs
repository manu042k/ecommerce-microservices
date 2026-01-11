using OrderService.Models;
using System.ComponentModel.DataAnnotations;

namespace OrderService.Dtos;

public class UpdateOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; set; }
}
