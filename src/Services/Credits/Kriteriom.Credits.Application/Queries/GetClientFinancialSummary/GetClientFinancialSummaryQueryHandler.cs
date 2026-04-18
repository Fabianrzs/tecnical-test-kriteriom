using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;

namespace Kriteriom.Credits.Application.Queries.GetClientFinancialSummary;

public class GetClientFinancialSummaryQueryHandler(
    IClientRepository clientRepository,
    ICreditRepository creditRepository)
    : IRequestHandler<GetClientFinancialSummaryQuery, Result<ClientFinancialSummaryDto>>
{
    public async Task<Result<ClientFinancialSummaryDto>> Handle(
        GetClientFinancialSummaryQuery query, CancellationToken ct)
    {
        var client = await clientRepository.GetByIdAsync(query.ClientId, ct);
        if (client is null)
            return Result<ClientFinancialSummaryDto>.Failure(
                $"Client {query.ClientId} not found", "CLIENT_NOT_FOUND");

        var activeCredits = await creditRepository.GetActiveCreditsForClientAsync(query.ClientId, ct);
        var list = activeCredits.ToList();

        var existingMonthlyDebt = list.Sum(c => c.MonthlyPayment());

        return Result<ClientFinancialSummaryDto>.Success(
            new ClientFinancialSummaryDto(existingMonthlyDebt, list.Count));
    }
}
