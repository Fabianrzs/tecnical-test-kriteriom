using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Domain;

namespace Kriteriom.Credits.Domain.Events;

public record CreditStatusChangedDomainEvent(
    Guid CreditId,
    CreditStatus OldStatus,
    CreditStatus NewStatus,
    DateTime UpdatedAt) : DomainEvent;
