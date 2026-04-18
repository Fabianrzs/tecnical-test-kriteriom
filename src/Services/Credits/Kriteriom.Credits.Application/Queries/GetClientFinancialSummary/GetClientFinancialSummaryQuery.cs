using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Queries.GetClientFinancialSummary;

public record GetClientFinancialSummaryQuery(Guid ClientId) : IQuery<Result<ClientFinancialSummaryDto>>;
