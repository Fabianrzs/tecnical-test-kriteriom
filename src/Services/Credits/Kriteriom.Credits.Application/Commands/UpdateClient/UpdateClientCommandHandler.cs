using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.UpdateClient;

public class UpdateClientCommandHandler(
    IClientRepository repository,
    ILogger<UpdateClientCommandHandler> logger) : IRequestHandler<UpdateClientCommand, Result<ClientDto>>
{
    public async Task<Result<ClientDto>> Handle(UpdateClientCommand command, CancellationToken ct)
    {
        var client = await repository.GetByIdAsync(command.ClientId, ct);
        if (client is null)
            return Result<ClientDto>.Failure($"Client {command.ClientId} not found", "CLIENT_NOT_FOUND");

        client.Update(command.FullName, command.MonthlyIncome, command.EmploymentStatus);
        await repository.UpdateAsync(client, ct);

        logger.LogInformation("Client updated. ClientId={ClientId}", client.Id);

        return Result<ClientDto>.Success(client.ToDto());
    }
}
