using PaymentService.Tests.Support;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Tests.Controllers;

public class PaymentWebhooksControllerTests : IClassFixture<PaymentServiceApplicationFactory>
{
    private readonly PaymentServiceApplicationFactory _factory;

    public PaymentWebhooksControllerTests(PaymentServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Receive_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = "{\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_test_123\"}}}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/payments/webhooks/stripe", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
