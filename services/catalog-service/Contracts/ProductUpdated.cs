namespace CatalogService.Contracts;

public record ProductUpdated(Guid Id, string Name, string Description, decimal Price, string ImageUrl, Guid CategoryId);
