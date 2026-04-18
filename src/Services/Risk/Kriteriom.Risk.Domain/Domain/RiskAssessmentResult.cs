namespace Kriteriom.Risk.Domain.Domain;

public record RiskAssessmentResult(
    Guid CreditId,
    decimal RiskScore,
    string Decision, // Approved, Rejected, Review
    string Reason
);
