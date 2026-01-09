using System.ComponentModel.DataAnnotations;

namespace CatalogService.Models;

public class Category
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
