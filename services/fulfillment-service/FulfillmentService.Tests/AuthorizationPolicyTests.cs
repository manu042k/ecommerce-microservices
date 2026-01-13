using FulfillmentService.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Linq;

namespace FulfillmentService.Tests;

public class AuthorizationPolicyTests : IClassFixture<FulfillmentServiceApplicationFactory>
{
    private readonly FulfillmentServiceApplicationFactory _factory;

    public AuthorizationPolicyTests(FulfillmentServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FulfillmentReadPolicy_RequiresCorrectRoles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policy = await provider.GetPolicyAsync("FulfillmentRead");

        // Assert
        policy.Should().NotBeNull();
        var rolesRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();
        rolesRequirement.AllowedRoles.Should().BeEquivalentTo(new[] { "Admin", "Ops", "Finance" });
    }

    [Fact]
    public async Task FulfillmentWritePolicy_RequiresCorrectRoles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policy = await provider.GetPolicyAsync("FulfillmentWrite");

        // Assert
        policy.Should().NotBeNull();
        var rolesRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();
        rolesRequirement.AllowedRoles.Should().BeEquivalentTo(new[] { "Admin", "Ops" });
    }
}
