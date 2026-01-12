using ApiGateway.Tests.Support;
using System.Linq;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Tests;

public class ReverseProxyConfigurationTests : IClassFixture<ApiGatewayApplicationFactory>
{
    private readonly ApiGatewayApplicationFactory _factory;

    public ReverseProxyConfigurationTests(ApiGatewayApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ReverseProxyLoadsCatalogPrefixedRouteWithTransform()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var configProvider = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();

        // Act
        var proxyConfig = configProvider.GetConfig();
        var catalogPrefixed = proxyConfig.Routes.Single(route => route.RouteId == "catalog-prefixed");

        // Assert
        catalogPrefixed.ClusterId.Should().Be("catalog-cluster");
        catalogPrefixed.Match.Path.Should().Be("/api/catalog/{**catch-all}");
        catalogPrefixed.Transforms.Should().NotBeNull();
        var catalogTransform = catalogPrefixed.Transforms!.Single();
        catalogTransform.Should().ContainKey("PathPattern");
        catalogTransform["PathPattern"].Should().Be("/api/{catch-all}");
    }

    [Fact]
    public void ReverseProxyDefinesServiceClustersWithExpectedDestinations()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var configProvider = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();

        // Act
        var proxyConfig = configProvider.GetConfig();
        var paymentCluster = proxyConfig.Clusters.Single(cluster => cluster.ClusterId == "payment-cluster");
        var identitySwaggerRoute = proxyConfig.Routes.Single(route => route.RouteId == "identity-swagger");

        // Assert
        paymentCluster.Destinations.Should().ContainKey("destination1");
        paymentCluster.Destinations["destination1"].Address.Should().Be("http://payment-service:8080");

        identitySwaggerRoute.Match.Path.Should().Be("/doc/identity/{**catch-all}");
        identitySwaggerRoute.ClusterId.Should().Be("identity-cluster");
        identitySwaggerRoute.Transforms.Should().NotBeNull();
        var swaggerTransform = identitySwaggerRoute.Transforms!.Single();
        swaggerTransform.Should().ContainKey("PathPattern");
        swaggerTransform["PathPattern"].Should().Be("/swagger/v1/{catch-all}");
    }
}
