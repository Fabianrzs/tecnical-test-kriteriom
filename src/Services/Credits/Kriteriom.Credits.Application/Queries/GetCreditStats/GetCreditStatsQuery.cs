using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using MediatR;

namespace Kriteriom.Credits.Application.Queries.GetCreditStats;

public record GetCreditStatsQuery : IRequest<Result<CreditStatsDto>>;
