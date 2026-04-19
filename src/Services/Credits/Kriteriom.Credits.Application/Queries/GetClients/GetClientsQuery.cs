using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetClients;

public record GetClientsQuery(
    int               Page             = 1,
    int               PageSize         = 20,
    string?           Search           = null,
    EmploymentStatus? EmploymentStatus = null,
    string?           ScoreTier        = null,
    decimal?          IncomeMin        = null,
    decimal?          IncomeMax        = null
) : IQuery<Result<PagedResult<ClientDto>>>, ICacheable
{
    public string CacheKey =>
        $"clients:p{Page}:s{PageSize}:q{Search}:emp{EmploymentStatus}" +
        $":sc{ScoreTier}:imin{IncomeMin}:imax{IncomeMax}";

    public TimeSpan CacheDuration => TimeSpan.FromSeconds(30);
}
