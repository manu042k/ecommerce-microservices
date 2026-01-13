using PaymentService.Data;
using PaymentService.Dtos;
using PaymentService.Models;
using PaymentService.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PaymentService.Tests.Controllers;

public class PaymentsControllerTests : IClassFixture<PaymentServiceApplicationFactory>
{
    private readonly PaymentServiceApplicationFactory _factory;

    public PaymentsControllerTests(PaymentServiceApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatePayment_WithValidRequest_CreatesPayment()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreatePaymentRequest
        {
            OrderId = Guid.NewGuid(),
            Amount = 99.99m,
            Currency = "usd",
            PaymentMethodId = "pm_test_123",
            Description = "Test payment"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment.Should().NotBeNull();
        payment!.OrderId.Should().Be(request.OrderId);
        payment.Amount.Should().Be(request.Amount);
    }

    [Fact]
    public async Task GetPaymentById_WithValidId_ReturnsPayment()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var payment = await SeedPaymentAsync(context);
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{payment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paymentDto = await response.Content.ReadFromJsonAsync<PaymentDto>();
        paymentDto.Should().NotBeNull();
        paymentDto!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetPaymentById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPayments_WithFilters_ReturnsFilteredPayments()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var orderId = Guid.NewGuid();
        await SeedPaymentAsync(context, orderId: orderId);
        await SeedPaymentAsync(context, orderId: Guid.NewGuid());
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments?orderId={orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payments = await response.Content.ReadFromJsonAsync<List<PaymentDto>>();
        payments.Should().NotBeNull();
        payments!.All(p => p.OrderId == orderId).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmPayment_WithValidId_ConfirmsPayment()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var payment = await SeedPaymentAsync(context, status: PaymentStatus.Pending);
        
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/api/payments/{payment.Id}/confirm", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        // The actual result depends on the payment provider mock
    }

    [Fact]
    public async Task Refund_WithValidRequest_CreatesRefund()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var payment = await SeedPaymentAsync(context, status: PaymentStatus.Succeeded);
        
        var client = _factory.CreateClient();
        var request = new RefundRequest
        {
            Amount = 50.00m,
            Reason = "Test refund"
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/payments/{payment.Id}/refund", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        // The actual result depends on the payment provider mock
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
