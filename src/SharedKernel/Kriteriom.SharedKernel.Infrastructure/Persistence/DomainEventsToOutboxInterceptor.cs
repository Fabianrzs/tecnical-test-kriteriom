using System.Text.Json;
using Kriteriom.SharedKernel.Application.Services;
using Kriteriom.SharedKernel.Domain;
using Kriteriom.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kriteriom.SharedKernel.Infrastructure.Persistence;

public class DomainEventsToOutboxInterceptor(IDomainEventMapper mapper) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        if (eventData.Context is not null)
            ConvertDomainEventsToOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void ConvertDomainEventsToOutboxMessages(DbContext context)
    {
        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.GetDomainEvents().Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.GetDomainEvents())
            {
                var integrationEvent = mapper.Map(domainEvent);
                if (integrationEvent is null) continue;

                var outboxMessage = new OutboxMessage
                {
                    Id         = Guid.NewGuid(),
                    EventType  = integrationEvent.GetType().Name,
                    Payload    = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions),
                    CreatedAt  = DateTime.UtcNow,
                    RetryCount = 0
                };

                context.Set<OutboxMessage>().Add(outboxMessage);
            }

            aggregate.ClearDomainEvents();
        }
    }
}
