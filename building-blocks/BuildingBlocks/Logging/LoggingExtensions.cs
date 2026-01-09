using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace BuildingBlocks.Logging;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddCustomLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .WriteTo.Console();
        });

        return builder;
    }

    public static WebApplication UseCustomLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        return app;
    }
}
