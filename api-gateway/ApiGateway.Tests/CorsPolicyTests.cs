using ApiGateway.Tests.Support;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace ApiGateway.Tests;

public class CorsPolicyTests : IClassFixture<ApiGatewayApplicationFactory>
{
    private readonly ApiGatewayApplicationFactory _factory;

    public CorsPolicyTests(ApiGatewayApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CorsPolicy_AllowsAnyOriginMethodAndHeader()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICorsPolicyProvider>();

        var httpContext = new DefaultHttpContext();

        // Act
        var policy = await provider.GetPolicyAsync(httpContext, "CorsPolicy");

        // Assert
        policy.Should().NotBeNull();
        policy!.AllowAnyOrigin.Should().BeTrue();
        policy.AllowAnyMethod.Should().BeTrue();
        policy.AllowAnyHeader.Should().BeTrue();
    }
}
