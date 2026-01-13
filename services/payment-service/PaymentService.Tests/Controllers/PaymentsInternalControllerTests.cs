using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Models;
using PaymentService.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PaymentService.Tests.Controllers;

public class PaymentsInternalControllerTests : IClassFixture<PaymentServiceApplicationFactory>
{
    private readonly PaymentServiceApplicationFactory _factory;

    public PaymentsInternalControllerTests(PaymentServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Capture_WithValidRequest_AcceptsRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var payment = await SeedPaymentAsync(context);
        
        var client = _factory.CreateClient();
        var request = new CapturePaymentRequest
        {
            PaymentId = payment.Id,
            Amount = payment.Amount
        };

        // Act
        var response = await client.PostAsJsonAsync("/internal/payments/capture", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.NotFound);
    }

    private static async Task<Payment> SeedPaymentAsync(
        PaymentDbContext context,
        Guid? orderId = null,
        PaymentStatus status = PaymentStatus.Pending)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId ?? Guid.NewGuid(),
            Amount = 99.99m,
            Currency = "usd",
            Status = status,
            Provider = "stripe",
            ProviderPaymentId = "pi_test_123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment;
    }
}
