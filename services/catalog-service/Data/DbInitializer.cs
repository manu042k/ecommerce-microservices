using CatalogService.Models;

namespace CatalogService.Data;

public static class DbInitializer
{
    public static void Initialize(CatalogDbContext context)
    {
        if (context.Products.Any())
        {
            return;   // DB has been seeded
        }

        var electronics = new Category { Id = Guid.NewGuid(), Name = "Electronics", Description = "Electronic devices and accessories" };
        var clothing = new Category { Id = Guid.NewGuid(), Name = "Clothing", Description = "Apparel for men and women" };
        var books = new Category { Id = Guid.NewGuid(), Name = "Books", Description = "Books and literature" };

        context.Categories.AddRange(electronics, clothing, books);

        var products = new Product[]
        {
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "iPhone 15 Pro",
                Description = "The latest iPhone with titanium design.",
                Price = 999.00m,
                ImageUrl = "https://example.com/images/iphone15pro.jpg",
                CategoryId = electronics.Id
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "MacBook Pro 14",
                Description = "M3 Pro chip, 18GB memory, 512GB SSD.",
                Price = 1999.00m,
                ImageUrl = "https://example.com/images/macbookpro14.jpg",
                CategoryId = electronics.Id
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Sony WH-1000XM5",
                Description = "Wireless Noise Cancelling Headphones.",
                Price = 348.00m,
                ImageUrl = "https://example.com/images/sonyheadphones.jpg",
                CategoryId = electronics.Id
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Men's Cotton T-Shirt",
                Description = "Classic fit, 100% cotton.",
                Price = 19.99m,
                ImageUrl = "https://example.com/images/tshirt.jpg",
                CategoryId = clothing.Id
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Design Patterns",
                Description = "Elements of Reusable Object-Oriented Software.",
                Price = 54.99m,
                ImageUrl = "https://example.com/images/designpatterns.jpg",
                CategoryId = books.Id
            }
        };

        context.Products.AddRange(products);
        context.SaveChanges();
    }
}
