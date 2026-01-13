using PaymentService.Tests.Support;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace PaymentService.Tests;

public class SwaggerConfigurationTests : IClassFixture<PaymentServiceApplicationFactory>
{
    private readonly PaymentServiceApplicationFactory _factory;

    public SwaggerConfigurationTests(PaymentServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void SwaggerDefinesOAuth2FlowForKeycloak()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        var securitySchemes = options.SwaggerGeneratorOptions.SecuritySchemes;

        // Act & Assert
        securitySchemes.Should().ContainKey("oauth2");
        var oauthScheme = securitySchemes["oauth2"];
        oauthScheme.Type.Should().Be(SecuritySchemeType.OAuth2);
        oauthScheme.Flows.AuthorizationCode.Should().NotBeNull();
        oauthScheme.Flows.AuthorizationCode.AuthorizationUrl.Should().NotBeNull();
        oauthScheme.Flows.AuthorizationCode.TokenUrl.Should().NotBeNull();
        oauthScheme.Flows.AuthorizationCode.Scopes.Keys.Should().Contain(new[] { "openid", "profile", "email" });
    }

    [Fact]
    public async Task SwaggerUiEndpoint_IsReachableInDevelopment()
    {
        // Arrange
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var response = await client.GetAsync("/swagger/index.html");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Payment Service API v1");
    }
}
