using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;

namespace Kriteriom.Credits.Application.Queries.GetClients;

public class GetClientsQueryHandler(IClientRepository repository) : IRequestHandler<GetClientsQuery, Result<PagedResult<ClientDto>>>
{
    public async Task<Result<PagedResult<ClientDto>>> Handle(GetClientsQuery query, CancellationToken ct)
    {
        var (items, total) = await repository.GetAllAsync(query.Page, query.PageSize, ct);

        var result = new PagedResult<ClientDto>
        {
            Items      = items.Select(c => c.ToDto()),
            TotalCount = total,
            Page       = query.Page,
            PageSize   = query.PageSize
        };

        return Result<PagedResult<ClientDto>>.Success(result);
    }
}
