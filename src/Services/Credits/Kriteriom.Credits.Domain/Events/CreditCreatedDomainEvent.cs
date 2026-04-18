using Kriteriom.SharedKernel.Domain;

namespace Kriteriom.Credits.Domain.Events;

public record CreditCreatedDomainEvent(
    Guid CreditId,
    Guid ClientId,
    decimal Amount,
    decimal InterestRate) : DomainEvent;
