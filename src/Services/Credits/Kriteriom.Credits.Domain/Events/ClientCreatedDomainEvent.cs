using Kriteriom.SharedKernel.Domain;

namespace Kriteriom.Credits.Domain.Events;

public record ClientCreatedDomainEvent(
    Guid ClientId,
    string FullName,
    string Email,
    string DocumentNumber,
    decimal MonthlyIncome,
    string EmploymentStatus) : DomainEvent;
