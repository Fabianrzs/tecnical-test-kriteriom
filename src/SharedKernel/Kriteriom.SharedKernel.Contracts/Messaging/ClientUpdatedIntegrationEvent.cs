namespace Kriteriom.SharedKernel.Messaging;

public record ClientUpdatedIntegrationEvent : IntegrationEvent
{
    public Guid ClientId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public decimal MonthlyIncome { get; init; }
    public string EmploymentStatus { get; init; } = string.Empty;
}
