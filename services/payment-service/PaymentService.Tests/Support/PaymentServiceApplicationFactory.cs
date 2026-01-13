using PaymentService.Data;
using MassTransit;
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

namespace PaymentService.Tests.Support;

public class PaymentServiceApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
        });

        builder.ConfigureServices(services =>
        {
            // Replace MassTransit RabbitMQ with in-memory bus
            var massTransitDescriptors = services.Where(d => 
                d.ServiceType.FullName?.Contains("MassTransit") == true ||
                d.ImplementationType?.FullName?.Contains("MassTransit") == true ||
                d.ImplementationInstance?.GetType().FullName?.Contains("MassTransit") == true).ToList();
            
            foreach (var descriptor in massTransitDescriptors)
            {
                services.Remove(descriptor);
            }
            
            services.AddMassTransit(x =>
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            });
            
            // Replace database with in-memory
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PaymentDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddDbContext<PaymentDbContext>(options =>
                options.UseInMemoryDatabase($"PaymentServiceTests_{Guid.NewGuid()}")
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Replace Redis cache with in-memory cache for testing
            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (redisDescriptor is not null)
            {
                services.Remove(redisDescriptor);
            }

            services.AddSingleton<IDistributedCache>(sp =>
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            
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
                    .RequireRole("Admin", "Customer", "Finance")
                    .Build();
                    
                options.AddPolicy("CustomersOrAdmin", policy => 
                    policy.AddAuthenticationSchemes("Test")
                          .RequireAuthenticatedUser()
                          .RequireRole("Customer", "User", "Admin"));
                          
                options.AddPolicy("FinanceOrAdmin", policy => 
                    policy.AddAuthenticationSchemes("Test")
                          .RequireAuthenticatedUser()
                          .RequireRole("Finance", "Admin"));
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
            new Claim(ClaimTypes.Role, "Customer"),
            new Claim(ClaimTypes.Role, "Finance")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
