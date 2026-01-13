using FulfillmentService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FulfillmentService.Tests.Support;

public class FulfillmentServiceApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
            // Override RabbitMQ config to prevent connection attempts
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RabbitMQ:Host", "localhost" },
                { "RabbitMQ:UserName", "guest" },
                { "RabbitMQ:Password", "guest" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace database with in-memory
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FulfillmentDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddDbContext<FulfillmentDbContext>(options =>
                options.UseInMemoryDatabase($"FulfillmentServiceTests_{Guid.NewGuid()}")
                       .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            // Replace Redis cache with in-memory cache for testing
            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (redisDescriptor is not null)
            {
                services.Remove(redisDescriptor);
            }

            services.AddSingleton<IDistributedCache>(sp =>
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

            // Note: MassTransit will try to connect to RabbitMQ during integration tests
            // This is acceptable as those tests don't actually need message bus functionality
            // Unit tests (ShipmentsControllerTests) mock IPublishEndpoint, so they work fine
        });
    }
}
