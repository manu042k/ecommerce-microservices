using OrderService.Data;
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
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrderService.Tests.Support;

public class OrderServiceApplicationFactory : WebApplicationFactory<Program>
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
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<OrderDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            services.AddDbContext<OrderDbContext>(options =>
                options.UseInMemoryDatabase($"OrderServiceTests_{Guid.NewGuid()}")
                       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Replace Redis cache with in-memory cache for testing
            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (redisDescriptor is not null)
            {
                services.Remove(redisDescriptor);
            }

            services.AddSingleton<IDistributedCache>(sp =>
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            
            // Replace HttpClientFactory to mock Catalog Service responses
            var httpClientFactoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHttpClientFactory));
            if (httpClientFactoryDescriptor is not null)
            {
                services.Remove(httpClientFactoryDescriptor);
            }
            
            services.AddSingleton<IHttpClientFactory>(sp => new TestHttpClientFactory());
            
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
                    .RequireRole("Admin", "User", "Customer")
                    .Build();
                    
                options.AddPolicy("AdminOnly", policy => 
                    policy.AddAuthenticationSchemes("Test")
                          .RequireAuthenticatedUser()
                          .RequireRole("Admin"));
                          
                options.AddPolicy("UserOrAdmin", policy => 
                    policy.AddAuthenticationSchemes("Test")
                          .RequireAuthenticatedUser()
                          .RequireRole("Customer", "User", "Admin"));
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
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

// Test HttpClientFactory that mocks Catalog Service responses
public class TestHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var handler = new TestCatalogServiceHandler();
        return new HttpClient(handler) { BaseAddress = new Uri("http://catalog-service:8080") };
    }
}

// Test HTTP handler that mocks Catalog Service product verification
public class TestCatalogServiceHandler : HttpClientHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/api/products/verify")
        {
            // Extract product IDs from query string
            var query = request.RequestUri.Query;
            var productIds = new List<Guid>();
            var parts = query.TrimStart('?').Split('&');
            foreach (var part in parts)
            {
                if (part.StartsWith("ids=") && Guid.TryParse(part.Substring(4), out var id))
                {
                    productIds.Add(id);
                }
            }
            
            // Return mock product data
            var products = productIds.Select(id => new
            {
                Id = id,
                Name = $"Test Product {id}",
                Price = 49.99m
            }).ToList();
            
            var json = JsonSerializer.Serialize(products);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
        
        // Default response
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
