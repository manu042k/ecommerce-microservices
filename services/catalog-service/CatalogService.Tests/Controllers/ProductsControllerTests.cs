using CatalogService.Controllers;
using CatalogService.Contracts;
using CatalogService.Data;
using CatalogService.Models;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading;
using System.Linq;

namespace CatalogService.Tests.Controllers;

public class ProductsControllerTests
{
    [Fact]
    public async Task GetProducts_NoFilters_ReturnsCachedResultsWhenDatabaseChanges()
    {
        // Arrange
        await using var context = CreateContext();
        var cache = CreateCache();
        var publishEndpoint = CreatePublishEndpoint();
        SeedProduct(context);
        var controller = new ProductsController(context, cache, publishEndpoint.Object);

        // Act
        var firstResponse = await controller.GetProducts(null, null, null, null, 1, 10);
        var existingProducts = context.Products.ToList();
        context.Products.RemoveRange(existingProducts);
        await context.SaveChangesAsync();
        var secondResponse = await controller.GetProducts(null, null, null, null, 1, 10);

        // Assert
        firstResponse.Value.Should().NotBeNull();
        secondResponse.Value.Should().NotBeNull();
        firstResponse.Value!.Should().HaveCount(1);
        secondResponse.Value!.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProduct_CachesItemAfterFirstFetch()
    {
        // Arrange
        await using var context = CreateContext();
        var cache = CreateCache();
        var publishEndpoint = CreatePublishEndpoint();
        var product = SeedProduct(context);
        var controller = new ProductsController(context, cache, publishEndpoint.Object);

        // Act
        var firstResult = await controller.GetProduct(product.Id);
        var existingProducts = context.Products.ToList();
        context.Products.RemoveRange(existingProducts);
        await context.SaveChangesAsync();
        var secondResult = await controller.GetProduct(product.Id);

        // Assert
        firstResult.Value.Should().NotBeNull();
        secondResult.Value.Should().NotBeNull();
        secondResult.Value!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task DeleteProduct_RemovesEntityAndInvalidatesCache()
    {
        // Arrange
        await using var context = CreateContext();
        var cache = CreateCache();
        var publishEndpoint = CreatePublishEndpoint();
        var product = SeedProduct(context);
        await cache.SetStringAsync($"product_{product.Id}", "cached");
        var controller = new ProductsController(context, cache, publishEndpoint.Object);

        // Act
        var result = await controller.DeleteProduct(product.Id);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        (await context.Products.FindAsync(product.Id)).Should().BeNull();
        (await cache.GetStringAsync($"product_{product.Id}")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateProduct_PublishesEventAndClearsCache()
    {
        // Arrange
        await using var context = CreateContext();
        var cache = CreateCache();
        var publishEndpoint = CreatePublishEndpoint();
        var product = SeedProduct(context);
        await cache.SetStringAsync($"product_{product.Id}", "cached");
        context.Entry(product).State = EntityState.Detached;
        var controller = new ProductsController(context, cache, publishEndpoint.Object);
        var updatedProduct = new Product
        {
            Id = product.Id,
            Name = "Updated",
            Description = "Updated",
            Price = product.Price + 10,
            CategoryId = product.CategoryId,
            ImageUrl = product.ImageUrl
        };

        // Act
        var result = await controller.UpdateProduct(product.Id, updatedProduct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        publishEndpoint.Verify(endpoint => endpoint.Publish(
            It.Is<ProductUpdated>(p => p.Id == product.Id && p.Price == updatedProduct.Price),
            It.IsAny<CancellationToken>()), Times.Once);
        (await cache.GetStringAsync($"product_{product.Id}")).Should().BeNull();
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

    private static Mock<IPublishEndpoint> CreatePublishEndpoint()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(p => p.Publish(It.IsAny<ProductUpdated>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return publishEndpoint;
    }

    private static Product SeedProduct(CatalogDbContext context)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "Desc",
            Price = 9.99m,
            CategoryId = category.Id,
            Category = category
        };

        context.Categories.Add(category);
        context.Products.Add(product);
        context.SaveChanges();
        return product;
    }
}
