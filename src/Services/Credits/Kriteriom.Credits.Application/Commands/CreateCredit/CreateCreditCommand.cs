using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Commands.CreateCredit;

public record CreateCreditCommand : ICommand<Result<CreditDto>>
{
    public Guid ClientId { get; init; }
    public decimal Amount { get; init; }
    public decimal InterestRate { get; init; }
    public int TermMonths { get; init; } = 36;
    public string IdempotencyKey { get; init; } = string.Empty;
}
