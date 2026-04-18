using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Mapping;
using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Application.Commands.CreateClient;

public class CreateClientCommandHandler(
    IClientRepository repository,
    ILogger<CreateClientCommandHandler> logger) : IRequestHandler<CreateClientCommand, Result<ClientDto>>
{
    public async Task<Result<ClientDto>> Handle(CreateClientCommand command, CancellationToken ct)
    {
        var existing = await repository.GetByEmailAsync(command.Email, ct);
        if (existing is not null)
            return Result<ClientDto>.Failure($"A client with email '{command.Email}' already exists", "CLIENT_DUPLICATE_EMAIL");

        var byDoc = await repository.GetByDocumentAsync(command.DocumentNumber, ct);
        if (byDoc is not null)
            return Result<ClientDto>.Failure($"A client with document '{command.DocumentNumber}' already exists", "CLIENT_DUPLICATE_DOCUMENT");

        var client = Client.Create(
            command.FullName,
            command.Email,
            command.DocumentNumber,
            command.MonthlyIncome,
            command.EmploymentStatus);

        await repository.AddAsync(client, ct);

        logger.LogInformation("Client created. ClientId={ClientId} Email={Email}", client.Id, client.Email);

        return Result<ClientDto>.Success(client.ToDto());
    }
}
