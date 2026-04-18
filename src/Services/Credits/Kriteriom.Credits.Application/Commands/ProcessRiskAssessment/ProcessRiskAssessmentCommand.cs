using Kriteriom.Credits.Application.DTOs;
using Kriteriom.SharedKernel.Common;
using Kriteriom.SharedKernel.CQRS;

namespace Kriteriom.Credits.Application.Commands.ProcessRiskAssessment;

public record ProcessRiskAssessmentCommand : ICommand<Result<CreditDto>>
{
    public Guid CreditId { get; init; }
    public decimal RiskScore { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
