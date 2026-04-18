using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Aggregates;

namespace Kriteriom.Credits.Application.Mapping;

public static class CreditMappingExtensions
{
    public static CreditDto ToDto(this Credit credit) => new()
    {
        Id = credit.Id,
        ClientId = credit.ClientId,
        Amount = credit.Amount,
        InterestRate = credit.InterestRate,
        TermMonths = credit.TermMonths,
        Status = credit.Status,
        CreatedAt = credit.CreatedAt,
        UpdatedAt = credit.UpdatedAt,
        RiskScore = credit.RiskScore,
        RejectionReason = credit.RejectionReason
    };
}
