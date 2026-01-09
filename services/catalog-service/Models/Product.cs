using System.ComponentModel.DataAnnotations;

namespace CatalogService.Models;

public class Product
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; } // Simplified for now, can be expanded to List<string> if needed or stored as JSON

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }
}
