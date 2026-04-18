namespace Kriteriom.SharedKernel.Messaging;

public record RiskAssessedIntegrationEvent : IntegrationEvent
{
    public Guid CreditId { get; init; }
    public decimal RiskScore { get; init; }
    public string Decision { get; init; } = string.Empty; // Approved, Rejected, Review
    public string Reason { get; init; } = string.Empty;
}
