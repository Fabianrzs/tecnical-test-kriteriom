using Kriteriom.SharedKernel.Domain;
using Kriteriom.SharedKernel.Messaging;

namespace Kriteriom.SharedKernel.Application.Services;

public interface IDomainEventMapper
{
    IntegrationEvent? Map(IDomainEvent domainEvent);
}
