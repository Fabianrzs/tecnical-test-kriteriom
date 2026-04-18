using Kriteriom.Credits.Domain.Enums;

namespace Kriteriom.Credits.Application.DTOs;

public class CreditDto
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public decimal Amount { get; init; }
    public decimal InterestRate { get; init; }
    public int TermMonths { get; init; }
    public CreditStatus Status { get; init; }
    public string StatusName => Status.ToString();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public decimal? RiskScore { get; init; }
    public string? RejectionReason { get; init; }
    public bool IsHighRisk => RiskScore.HasValue && RiskScore.Value > 70;
}
