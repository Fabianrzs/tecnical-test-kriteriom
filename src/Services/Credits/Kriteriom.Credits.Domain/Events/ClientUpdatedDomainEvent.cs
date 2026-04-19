using Kriteriom.SharedKernel.Domain;

namespace Kriteriom.Credits.Domain.Events;

public record ClientUpdatedDomainEvent(
    Guid ClientId,
    string FullName,
    decimal MonthlyIncome,
    string EmploymentStatus) : DomainEvent;
