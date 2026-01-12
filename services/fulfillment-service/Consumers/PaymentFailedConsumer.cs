using BuildingBlocks.Contracts.Payments;
using FulfillmentService.Services;
using MassTransit;

namespace FulfillmentService.Consumers;

public class PaymentFailedConsumer : IConsumer<IPaymentFailed>
{
    private readonly IFulfillmentService _fulfillmentService;

    public PaymentFailedConsumer(IFulfillmentService fulfillmentService)
    {
        _fulfillmentService = fulfillmentService;
    }

    public async Task Consume(ConsumeContext<IPaymentFailed> context)
    {
        await _fulfillmentService.HandlePaymentFailedAsync(
            context.Message.OrderId,
            context.Message.PaymentId,
            context.Message.ErrorCode,
            context.Message.ErrorMessage,
            context.CancellationToken);
    }
}
