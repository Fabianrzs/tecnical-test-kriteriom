using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;

namespace Kriteriom.Credits.Domain.Specifications;

public sealed class CreditsByStatusSpecification : BaseSpecification<Credit>
{
    public CreditsByStatusSpecification(CreditStatus status)
        : base(c => c.Status == status && c.Amount > 0)
    {
        ApplyOrderByDescending(c => c.CreatedAt);
    }
}
