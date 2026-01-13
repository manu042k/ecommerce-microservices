using CatalogService.Controllers;
using CatalogService.Data;
using CatalogService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Linq;
using System;

namespace CatalogService.Tests.Controllers;

public class CategoriesControllerTests
{
    [Fact]
    public async Task GetCategories_ReturnsCachedResultsWhenDataChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var cache = CreateCache();
        context.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Accessories" });
        await context.SaveChangesAsync();
        var controller = new CategoriesController(context, cache);

        // Act
        var firstResponse = await controller.GetCategories();
        var existingCategories = context.Categories.ToList();
        context.Categories.RemoveRange(existingCategories);
        await context.SaveChangesAsync();
        var secondResponse = await controller.GetCategories();

        // Assert
        firstResponse.Value.Should().NotBeNull();
        secondResponse.Value.Should().NotBeNull();
        firstResponse.Value!.Should().HaveCount(1);
        secondResponse.Value!.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateCategory_PersistsEntityAndClearsCache()
    {
        // Arrange
        await using var context = CreateContext();
        var cache = CreateCache();
        await cache.SetStringAsync("categories_all", "cached");
        var controller = new CategoriesController(context, cache);
        var category = new Category { Id = Guid.NewGuid(), Name = "Shoes" };

        // Act
        var response = await controller.CreateCategory(category);

        // Assert
        response.Result.Should().BeOfType<CreatedAtActionResult>();
        context.Categories.Any().Should().BeTrue();
        (await cache.GetStringAsync("categories_all")).Should().BeNull();
    }

    private static CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CatalogDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static IDistributedCache CreateCache()
    {
        return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    }
}
