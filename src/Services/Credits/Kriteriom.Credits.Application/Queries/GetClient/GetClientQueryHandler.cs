using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;

namespace Kriteriom.Credits.Application.Queries.GetClient;

public class GetClientQueryHandler(IClientRepository repository) : IRequestHandler<GetClientQuery, Result<ClientDto>>
{
    public async Task<Result<ClientDto>> Handle(GetClientQuery query, CancellationToken ct)
    {
        var client = await repository.GetByIdAsync(query.ClientId, ct);
        if (client is null)
            return Result<ClientDto>.Failure($"Client {query.ClientId} not found", "CLIENT_NOT_FOUND");

        return Result<ClientDto>.Success(client.ToDto());
    }
}
