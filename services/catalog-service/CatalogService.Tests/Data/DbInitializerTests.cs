using CatalogService.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace CatalogService.Tests.Data;

public class DbInitializerTests
{
    [Fact]
    public void Initialize_SeedsCategoriesAndProducts()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new CatalogDbContext(options);

        // Act
        DbInitializer.Initialize(context);

        // Assert
        context.Categories.Count().Should().BeGreaterThan(0);
        context.Products.Count().Should().BeGreaterThan(0);
    }
}
