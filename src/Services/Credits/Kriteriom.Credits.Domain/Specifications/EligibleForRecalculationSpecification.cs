using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;

namespace Kriteriom.Credits.Domain.Specifications;

/// <summary>
/// Credits eligible for batch risk recalculation: active credits older than the staleness window.
/// </summary>
public sealed class EligibleForRecalculationSpecification : BaseSpecification<Credit>
{
    public EligibleForRecalculationSpecification(TimeSpan stalenessWindow)
        : base(c => c.Status == CreditStatus.Active
                    && c.UpdatedAt < DateTime.UtcNow - stalenessWindow)
    {
        ApplyOrderBy(c => c.UpdatedAt);
    }
}
