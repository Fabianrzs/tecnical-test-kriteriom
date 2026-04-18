using Kriteriom.Credits.Domain.Aggregates;

namespace Kriteriom.Credits.Domain.Specifications;

public sealed class CreditsForClientSpecification : BaseSpecification<Credit>
{
    public CreditsForClientSpecification(Guid clientId)
        : base(c => c.ClientId == clientId && c.Amount > 0)
    {
        ApplyOrderByDescending(c => c.CreatedAt);
    }
}
