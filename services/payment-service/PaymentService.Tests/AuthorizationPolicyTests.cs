using PaymentService.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Linq;

namespace PaymentService.Tests;

public class AuthorizationPolicyTests : IClassFixture<PaymentServiceApplicationFactory>
{
    private readonly PaymentServiceApplicationFactory _factory;

    public AuthorizationPolicyTests(PaymentServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomersOrAdminPolicy_AllowsMultipleRoles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policy = await provider.GetPolicyAsync("CustomersOrAdmin");

        // Assert
        policy.Should().NotBeNull();
        var rolesRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();
        rolesRequirement.AllowedRoles.Should().BeEquivalentTo(new[] { "Customer", "User", "Admin" });
    }

    [Fact]
    public async Task FinanceOrAdminPolicy_AllowsFinanceAndAdminRoles()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policy = await provider.GetPolicyAsync("FinanceOrAdmin");

        // Assert
        policy.Should().NotBeNull();
        var rolesRequirement = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single();
        rolesRequirement.AllowedRoles.Should().BeEquivalentTo(new[] { "Finance", "Admin" });
    }
}
