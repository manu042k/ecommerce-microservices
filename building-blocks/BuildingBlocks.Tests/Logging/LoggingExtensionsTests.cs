using BuildingBlocks.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.AspNetCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BuildingBlocks.Tests.Logging;

public class LoggingExtensionsTests
{
    [Fact]
    public async Task AddCustomLogging_RegistersSerilogLoggerFactory()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(LoggingExtensionsTests).Assembly.FullName
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Serilog:MinimumLevel:Default"] = "Information"
        });

        // Act
        builder.AddCustomLogging();
        await using var app = builder.Build();

        // Assert
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        loggerFactory.GetType().FullName.Should().Be("Serilog.Extensions.Logging.SerilogLoggerFactory");

        Log.CloseAndFlush();
    }

    [Fact]
    public async Task UseCustomLogging_RegistersDiagnosticContextAndMiddleware()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(LoggingExtensionsTests).Assembly.FullName
        });

        builder.WebHost.UseTestServer();
        builder.AddCustomLogging();

        // Act
        await using var app = builder.Build();
        app.UseCustomLogging();
        app.MapGet("/ping", () => "pong");

        await app.StartAsync();
        var client = app.GetTestClient();
        var response = await client.GetAsync("/ping");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var diagnosticContext = app.Services.GetService<IDiagnosticContext>();
        diagnosticContext.Should().NotBeNull();

        await app.StopAsync();
        Log.CloseAndFlush();
    }
}
