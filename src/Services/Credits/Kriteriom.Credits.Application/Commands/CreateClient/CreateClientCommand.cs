using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Commands.CreateClient;

public record CreateClientCommand : ICommand<Result<ClientDto>>
{
    public string FullName         { get; init; } = string.Empty;
    public string Email            { get; init; } = string.Empty;
    public string DocumentNumber   { get; init; } = string.Empty;
    public decimal MonthlyIncome   { get; init; }
    public EmploymentStatus EmploymentStatus { get; init; }
}
