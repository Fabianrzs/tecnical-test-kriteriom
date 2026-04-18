using Kriteriom.Credits.Application.Commands.RecalculateCreditStatuses;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using MediatR;

namespace Kriteriom.Credits.API.Consumers;

public class RecalculateCreditStatusesConsumer(
    IMediator mediator,
    ILogger<RecalculateCreditStatusesConsumer> logger)
    : IConsumer<RecalculateCreditStatusesRequestedEvent>
{
    public async Task Consume(ConsumeContext<RecalculateCreditStatusesRequestedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation("Recalculation requested. BatchSize={BatchSize}", msg.BatchSize);

        var result = await mediator.Send(
            new RecalculateCreditStatusesCommand(msg.BatchSize),
            context.CancellationToken);

        if (result.IsSuccess)
            logger.LogInformation(
                "Recalculation completed. Processed={Processed}, Updated={Updated}, Errors={Errors}",
                result.Value!.Processed, result.Value.Updated, result.Value.Errors);
        else
            logger.LogError("Recalculation failed: {Error}", result.Error);
    }
}
