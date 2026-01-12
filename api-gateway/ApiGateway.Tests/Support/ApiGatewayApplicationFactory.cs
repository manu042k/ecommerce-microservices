using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Tests.Support;

public class ApiGatewayApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        });
    }
}
