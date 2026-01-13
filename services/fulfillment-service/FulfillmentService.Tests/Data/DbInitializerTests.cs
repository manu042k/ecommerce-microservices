using FulfillmentService.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace FulfillmentService.Tests.Data;

public class DbInitializerTests
{
    [Fact]
    public async Task Initialize_CreatesDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new FulfillmentDbContext(options);

        // Act
        await DbInitializer.InitializeAsync(context);

        // Assert
        context.Database.EnsureCreated().Should().BeTrue();
    }
}
