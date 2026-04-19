using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Exceptions;

namespace Kriteriom.Tests.Domain;

public class CreditTests
{
    private static readonly Guid ValidClientId = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParams_ReturnsCredit()
    {
        var credit = Credit.Create(ValidClientId, 5_000_000m, 0.12m, 36);

        credit.ClientId.Should().Be(ValidClientId);
        credit.Amount.Should().Be(5_000_000m);
        credit.InterestRate.Should().Be(0.12m);
        credit.TermMonths.Should().Be(36);
        credit.Status.Should().Be(CreditStatus.Pending);
        credit.RiskScore.Should().BeNull();
    }

    [Fact]
    public void Create_WithDefaultTerm_UsesThirtySixMonths()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m);
        credit.TermMonths.Should().Be(36);
    }

    [Fact]
    public void Create_RaisesCreatedDomainEvent()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 24);
        credit.GetDomainEvents().Should().HaveCount(1);
    }

    [Fact]
    public void Create_DomainEventCarriesEnrichmentData()
    {
        var credit = Credit.Create(
            ValidClientId, 2_000_000m, 0.12m, 36,
            monthlyIncome: 5_000_000m, existingMonthlyDebt: 100_000m, clientCreditScore: 720);

        var e = credit.GetDomainEvents()[0]
            .Should().BeOfType<Kriteriom.Credits.Domain.Events.CreditCreatedDomainEvent>().Subject;

        e.MonthlyIncome.Should().Be(5_000_000m);
        e.ExistingMonthlyDebt.Should().Be(100_000m);
        e.ClientCreditScore.Should().Be(720);
        e.TermMonths.Should().Be(36);
    }

    [Fact]
    public void AssignRiskScore_RaisesStatusChangedEvent()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        credit.ClearDomainEvents();

        credit.AssignRiskScore(15m, RiskDecision.Approved);

        var events = credit.GetDomainEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<Kriteriom.Credits.Domain.Events.CreditStatusChangedDomainEvent>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        var act = () => Credit.Create(ValidClientId, amount, 0.10m, 36);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_WithInvalidInterestRate_Throws(decimal rate)
    {
        var act = () => Credit.Create(ValidClientId, 1_000_000m, rate, 36);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Fact]
    public void Create_WithEmptyClientId_Throws()
    {
        var act = () => Credit.Create(Guid.Empty, 1_000_000m, 0.10m, 36);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(121)]
    public void Create_WithTermOutsideRange_Throws(int term)
    {
        var act = () => Credit.Create(ValidClientId, 1_000_000m, 0.10m, term);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    // ── AssignRiskScore ───────────────────────────────────────────────────────

    [Fact]
    public void AssignRiskScore_Approved_SetsStatusActive()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        credit.AssignRiskScore(10m, RiskDecision.Approved);

        credit.Status.Should().Be(CreditStatus.Active);
        credit.RiskScore.Should().Be(10m);
    }

    [Fact]
    public void AssignRiskScore_Rejected_SetsStatusRejected()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        credit.AssignRiskScore(85m, RiskDecision.Rejected);

        credit.Status.Should().Be(CreditStatus.Rejected);
        credit.RiskScore.Should().Be(85m);
    }

    [Fact]
    public void AssignRiskScore_UnderReview_SetsStatusUnderReview()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        credit.AssignRiskScore(45m, RiskDecision.UnderReview);

        credit.Status.Should().Be(CreditStatus.UnderReview);
        credit.RiskScore.Should().Be(45m);
    }

    [Fact]
    public void AssignRiskScore_WhenAlreadyActive_Throws()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        credit.AssignRiskScore(10m, RiskDecision.Approved);

        var act = () => credit.AssignRiskScore(5m, RiskDecision.Approved);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AssignRiskScore_WithScoreOutOfRange_Throws(decimal score)
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        var act = () => credit.AssignRiskScore(score, RiskDecision.Approved);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    // ── UpdateStatus ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_ValidTransition_ChangesStatus()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);
        credit.AssignRiskScore(10m, RiskDecision.Approved);

        credit.UpdateStatus(CreditStatus.Closed);
        credit.Status.Should().Be(CreditStatus.Closed);
    }

    [Fact]
    public void UpdateStatus_InvalidTransition_Throws()
    {
        var credit = Credit.Create(ValidClientId, 1_000_000m, 0.10m, 36);

        var act = () => credit.UpdateStatus(CreditStatus.Closed);
        act.Should().Throw<InvalidCreditOperationException>()
            .WithMessage("*Transición de estado no permitida*");
    }

    // ── MonthlyPayment ────────────────────────────────────────────────────────

    [Fact]
    public void MonthlyPayment_WithZeroRate_ReturnsAmountDividedByTerm()
    {
        var credit = Credit.Create(ValidClientId, 1_200_000m, 0m, 12);
        credit.MonthlyPayment().Should().Be(100_000m);
    }

    [Fact]
    public void MonthlyPayment_WithPositiveRate_IsGreaterThanZeroRate()
    {
        var creditWithRate = Credit.Create(ValidClientId, 1_200_000m, 0.12m, 12);
        var zeroRatePayment = 1_200_000m / 12m;

        creditWithRate.MonthlyPayment().Should().BeGreaterThan(zeroRatePayment);
    }

    [Fact]
    public void ComputeMonthlyPayment_Static_MatchesInstanceMethod()
    {
        var credit = Credit.Create(ValidClientId, 5_000_000m, 0.18m, 48);
        var staticResult = Credit.ComputeMonthlyPayment(5_000_000m, 0.18m, 48);

        credit.MonthlyPayment().Should().Be(staticResult);
    }
}
