using ApiGateway.Tests.Support;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace ApiGateway.Tests;

public class AuthenticationConfigurationTests : IClassFixture<ApiGatewayApplicationFactory>
{
    private readonly ApiGatewayApplicationFactory _factory;

    public AuthenticationConfigurationTests(ApiGatewayApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void JwtBearerOptions_AreLoadedFromKeycloakConfiguration()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        // Act
        var options = monitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        options.Authority.Should().Be("http://keycloak:8080/realms/ecommerce-realm");
        options.Audience.Should().Be("order-service");
        options.RequireHttpsMetadata.Should().BeFalse();

        options.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        options.TokenValidationParameters.ValidAudience.Should().Be("order-service");
        options.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
        options.TokenValidationParameters.ValidIssuer.Should().Be("http://keycloak:8080/realms/ecommerce-realm");
        options.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
    }
}
