using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Commands.UpdateCreditStatus;

public record UpdateCreditStatusCommand : ICommand<Result<CreditDto>>
{
    public Guid CreditId { get; init; }
    public CreditStatus NewStatus { get; init; }
    public string? Reason { get; init; }
}
