using BuildingBlocks.Contracts.Payments;
using FulfillmentService.Services;
using MassTransit;

namespace FulfillmentService.Consumers;

public class PaymentSucceededConsumer : IConsumer<IPaymentSucceeded>
{
    private readonly IFulfillmentService _fulfillmentService;

    public PaymentSucceededConsumer(IFulfillmentService fulfillmentService)
    {
        _fulfillmentService = fulfillmentService;
    }

    public async Task Consume(ConsumeContext<IPaymentSucceeded> context)
    {
        await _fulfillmentService.HandlePaymentSucceededAsync(
            context.Message.OrderId,
            context.Message.PaymentId,
            context.Message.Amount,
            context.Message.Currency,
            context.CancellationToken);
    }
}
