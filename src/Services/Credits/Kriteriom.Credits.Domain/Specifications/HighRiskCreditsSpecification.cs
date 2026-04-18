using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;

namespace Kriteriom.Credits.Domain.Specifications;

/// <summary>
/// Selects credits with a risk score above the given threshold that are still active
/// — used by the batch recalculation job to detect newly risky accounts.
/// </summary>
public sealed class HighRiskCreditsSpecification : BaseSpecification<Credit>
{
    public HighRiskCreditsSpecification(decimal riskThreshold = 70m)
        : base(c => c.RiskScore != null
                    && c.RiskScore > riskThreshold
                    && c.Status == CreditStatus.Active)
    {
        ApplyOrderByDescending(c => c.RiskScore!);
    }
}
