using InventoryService.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventoryService.Tests.Support;

public class InventoryServiceApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
            // MassTransit will attempt to connect to RabbitMQ during integration tests.
            // This is acceptable for integration tests as it mirrors production setup.
            // For unit tests, MassTransit is mocked.
        });

        builder.ConfigureServices(services =>
        {
            // Replace database with in-memory
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<InventoryDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            // Use a shared database name so all tests in the same factory instance share the same database
            var dbName = $"InventoryServiceTests_{Guid.NewGuid()}";
            services.AddDbContext<InventoryDbContext>(options =>
                options.UseInMemoryDatabase(dbName)
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Replace Redis cache with in-memory cache for testing
            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (redisDescriptor is not null)
            {
                services.Remove(redisDescriptor);
            }

            services.AddSingleton<IDistributedCache>(sp =>
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            
            // Remove existing authentication and add test authentication
            var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService));
            if (authDescriptor is not null)
            {
                services.Remove(authDescriptor);
            }
            
            // Add a test authentication scheme that always succeeds
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
            
            // Override default authentication scheme
            services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            });
            
            // Configure authorization policies to use Test scheme
            services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
            {
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Test")
                    .RequireAuthenticatedUser()
                    .RequireRole("Admin", "Ops")
                    .Build();
                    
                options.AddPolicy("AdminOnly", policy => 
                    policy.AddAuthenticationSchemes("Test")
                          .RequireAuthenticatedUser()
                          .RequireRole("Admin"));
                          
                options.AddPolicy("OpsOrAdmin", policy => 
                    policy.AddAuthenticationSchemes("Test")
                          .RequireAuthenticatedUser()
                          .RequireRole("Ops", "Admin"));
            });
        });
    }
}

// Test authentication handler that always authenticates
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder,
        Microsoft.AspNetCore.Authentication.ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "testuser"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Ops")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
