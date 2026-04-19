using Kriteriom.Credits.Application.Commands.ProcessRiskAssessment;
using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kriteriom.Tests.Application;

public class ProcessRiskAssessmentCommandHandlerTests
{
    private readonly ICreditRepository _creditRepo = Substitute.For<ICreditRepository>();
    private readonly IClientRepository _clientRepo = Substitute.For<IClientRepository>();
    private readonly ProcessRiskAssessmentCommandHandler _handler;

    public ProcessRiskAssessmentCommandHandlerTests()
    {
        _handler = new ProcessRiskAssessmentCommandHandler(
            _creditRepo, _clientRepo,
            NullLogger<ProcessRiskAssessmentCommandHandler>.Instance);
    }

    private static Credit MakePendingCredit(Guid? clientId = null)
        => Credit.Create(clientId ?? Guid.NewGuid(), 5_000_000m, 0.12m, 36);

    [Fact]
    public async Task Handle_CreditNotFound_ReturnsCreditNotFoundFailure()
    {
        var command = new ProcessRiskAssessmentCommand { CreditId = Guid.NewGuid(), RiskScore = 10m, Decision = "Approved", Reason = "OK" };
        _creditRepo.GetByIdAsync(command.CreditId, Arg.Any<CancellationToken>()).Returns((Credit?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CREDIT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_InvalidDecision_ReturnsInvalidRiskDecisionFailure()
    {
        var credit = MakePendingCredit();
        _creditRepo.GetByIdAsync(credit.Id, Arg.Any<CancellationToken>()).Returns(credit);
        var command = new ProcessRiskAssessmentCommand { CreditId = credit.Id, RiskScore = 10m, Decision = "INVALID", Reason = "?" };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_RISK_DECISION");
    }

    [Fact]
    public async Task Handle_ApprovedDecision_SetsStatusActive()
    {
        var credit = MakePendingCredit();
        _creditRepo.GetByIdAsync(credit.Id, Arg.Any<CancellationToken>()).Returns(credit);
        var client = Client.Create("Test", "t@t.com", "123", 10_000_000m, EmploymentStatus.Employed);
        _clientRepo.GetByIdAsync(credit.ClientId, Arg.Any<CancellationToken>()).Returns(client);

        var command = new ProcessRiskAssessmentCommand
        {
            CreditId = credit.Id, RiskScore = 10m, Decision = "Approved", Reason = "OK"
        };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CreditStatus.Active);
        result.Value.RiskScore.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_RejectedDecision_SetsStatusRejected()
    {
        var credit = MakePendingCredit();
        _creditRepo.GetByIdAsync(credit.Id, Arg.Any<CancellationToken>()).Returns(credit);
        var command = new ProcessRiskAssessmentCommand
        {
            CreditId = credit.Id, RiskScore = 90m, Decision = "Rejected", Reason = "High risk"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CreditStatus.Rejected);
    }

    [Fact]
    public async Task Handle_UnderReviewDecision_SetsStatusUnderReview()
    {
        var credit = MakePendingCredit();
        _creditRepo.GetByIdAsync(credit.Id, Arg.Any<CancellationToken>()).Returns(credit);
        var command = new ProcessRiskAssessmentCommand
        {
            CreditId = credit.Id, RiskScore = 45m, Decision = "UnderReview", Reason = "Needs review"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CreditStatus.UnderReview);
    }

    [Fact]
    public async Task Handle_Approved_AppliesDebtPenaltyToClient()
    {
        var client = Client.Create("Test", "t@t.com", "123", 10_000_000m, EmploymentStatus.Employed);
        var credit = MakePendingCredit(client.Id);
        var scoreBefore = client.CreditScore;

        _creditRepo.GetByIdAsync(credit.Id, Arg.Any<CancellationToken>()).Returns(credit);
        _clientRepo.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        var command = new ProcessRiskAssessmentCommand
        {
            CreditId = credit.Id, RiskScore = 5m, Decision = "Approved", Reason = "OK"
        };

        await _handler.Handle(command, CancellationToken.None);

        client.CreditScore.Should().BeLessThan(scoreBefore);
        await _clientRepo.Received(1).UpdateAsync(client, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyEvaluated_ReturnsSuccess_WithoutReprocessing()
    {
        var credit = MakePendingCredit();
        credit.AssignRiskScore(10m, RiskDecision.Approved); // Already applied

        _creditRepo.GetByIdAsync(credit.Id, Arg.Any<CancellationToken>()).Returns(credit);

        var command = new ProcessRiskAssessmentCommand
        {
            CreditId = credit.Id, RiskScore = 10m, Decision = "Approved", Reason = "OK"
        };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _creditRepo.DidNotReceive().UpdateAsync(Arg.Any<Credit>(), Arg.Any<CancellationToken>());
    }
}
