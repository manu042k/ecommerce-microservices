using PaymentService.Tests.Support;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace PaymentService.Tests;

public class CorsPolicyTests : IClassFixture<PaymentServiceApplicationFactory>
{
    private readonly PaymentServiceApplicationFactory _factory;

    public CorsPolicyTests(PaymentServiceApplicationFactory factory)
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
