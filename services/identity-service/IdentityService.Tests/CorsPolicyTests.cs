using IdentityService.Tests.Support;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace IdentityService.Tests;

public class CorsPolicyTests : IClassFixture<IdentityServiceApplicationFactory>
{
    private readonly IdentityServiceApplicationFactory _factory;

    public CorsPolicyTests(IdentityServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AllowAllPolicy_PermitsAnyOriginMethodHeader()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICorsPolicyProvider>();
        var context = new DefaultHttpContext();

        // Act
        var policy = await provider.GetPolicyAsync(context, "AllowAll");

        // Assert
        policy.Should().NotBeNull();
        policy!.AllowAnyOrigin.Should().BeTrue();
        policy.AllowAnyMethod.Should().BeTrue();
        policy.AllowAnyHeader.Should().BeTrue();
    }
}
