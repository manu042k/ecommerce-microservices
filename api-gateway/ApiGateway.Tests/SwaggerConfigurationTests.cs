using ApiGateway.Tests.Support;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApiGateway.Tests;

public class SwaggerConfigurationTests : IClassFixture<ApiGatewayApplicationFactory>
{
    private readonly ApiGatewayApplicationFactory _factory;

    public SwaggerConfigurationTests(ApiGatewayApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void SwaggerIncludesBearerAndOAuthSecurityDefinitions()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var securitySchemes = options.SwaggerGeneratorOptions.SecuritySchemes;

        // Act & Assert
        securitySchemes.ContainsKey("Bearer").Should().BeTrue("Bearer token auth is required for downstream services");
        securitySchemes.ContainsKey("OAuth2").Should().BeTrue("OAuth2 redirect flow should be discoverable in Swagger");

        var oauthScheme = securitySchemes["OAuth2"];
        oauthScheme.Flows.AuthorizationCode.Should().NotBeNull();
        oauthScheme.Flows.AuthorizationCode.AuthorizationUrl.Should().NotBeNull();
        oauthScheme.Flows.AuthorizationCode.TokenUrl.Should().NotBeNull();
        oauthScheme.Flows.AuthorizationCode.Scopes.Should().ContainKeys("openid", "profile");
    }

    [Fact]
    public async Task SwaggerUi_IsServedFromGatewayWithDocLinks()
    {
        // Arrange
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var response = await client.GetAsync("/swagger/index.html");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("doc/identity/swagger.json");
        html.Should().Contain("doc/catalog/swagger.json");
    }
}
