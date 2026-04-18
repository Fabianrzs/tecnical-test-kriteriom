using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Commands.UpdateClient;

public record UpdateClientCommand : ICommand<Result<ClientDto>>
{
    public Guid   ClientId         { get; init; }
    public string FullName         { get; init; } = string.Empty;
    public decimal MonthlyIncome   { get; init; }
    public EmploymentStatus EmploymentStatus { get; init; }
}
