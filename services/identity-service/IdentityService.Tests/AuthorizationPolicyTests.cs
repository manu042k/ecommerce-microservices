using IdentityService.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Linq;

namespace IdentityService.Tests;

public class AuthorizationPolicyTests : IClassFixture<IdentityServiceApplicationFactory>
{
    private readonly IdentityServiceApplicationFactory _factory;

    public AuthorizationPolicyTests(IdentityServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminOnlyPolicy_RequiresAdminRole()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policy = await provider.GetPolicyAsync("AdminOnly");

        // Assert
        policy.Should().NotBeNull();
        var rolesRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();
        rolesRequirement.AllowedRoles.Should().ContainSingle("Admin");
    }

    [Fact]
    public async Task UserOrAdminPolicy_AllowsMultipleRoles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policy = await provider.GetPolicyAsync("UserOrAdmin");

        // Assert
        policy.Should().NotBeNull();
        var rolesRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();
        rolesRequirement.AllowedRoles.Should().BeEquivalentTo(new[] { "Customer", "User", "Admin" });
    }
}
