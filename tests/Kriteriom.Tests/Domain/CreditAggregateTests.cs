using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Exceptions;
using static Kriteriom.Credits.Domain.Enums.RiskDecision;

namespace Kriteriom.Tests.Domain;

public class CreditAggregateTests
{
    private static readonly Guid ValidClientId = Guid.NewGuid();
    private const decimal ValidAmount = 5_000_000m;
    private const decimal ValidRate = 0.18m;
    private const int ValidTerm = 36;

    [Fact]
    public void Create_WithValidParams_ReturnsCreditWithPendingStatus()
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);

        credit.Status.Should().Be(CreditStatus.Pending);
        credit.ClientId.Should().Be(ValidClientId);
        credit.Amount.Should().Be(ValidAmount);
        credit.InterestRate.Should().Be(ValidRate);
        credit.TermMonths.Should().Be(ValidTerm);
        credit.RiskScore.Should().BeNull();
        credit.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100_000)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        var act = () => Credit.Create(ValidClientId, amount, ValidRate, ValidTerm);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(2.0)]
    public void Create_WithInvalidInterestRate_Throws(decimal rate)
    {
        var act = () => Credit.Create(ValidClientId, ValidAmount, rate, ValidTerm);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Fact]
    public void Create_WithEmptyClientId_Throws()
    {
        var act = () => Credit.Create(Guid.Empty, ValidAmount, ValidRate, ValidTerm);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(121)]
    [InlineData(0)]
    public void Create_WithInvalidTermMonths_Throws(int term)
    {
        var act = () => Credit.Create(ValidClientId, ValidAmount, ValidRate, term);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(6)]
    [InlineData(36)]
    [InlineData(120)]
    public void Create_WithValidTermBoundaries_Succeeds(int term)
    {
        var act = () => Credit.Create(ValidClientId, ValidAmount, ValidRate, term);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssignRiskScore_Approved_SetsStatusActive()
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);
        credit.AssignRiskScore(15.5m, Approved);
        credit.Status.Should().Be(CreditStatus.Active);
        credit.RiskScore.Should().Be(15.5m);
    }

    [Fact]
    public void AssignRiskScore_Rejected_SetsStatusRejected()
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);
        credit.AssignRiskScore(85m, Rejected);
        credit.Status.Should().Be(CreditStatus.Rejected);
    }

    [Fact]
    public void AssignRiskScore_UnderReview_SetsStatusUnderReview()
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);
        credit.AssignRiskScore(45m, UnderReview);
        credit.Status.Should().Be(CreditStatus.UnderReview);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AssignRiskScore_OutOfRange_Throws(decimal score)
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);
        var act = () => credit.AssignRiskScore(score, Approved);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Fact]
    public void MonthlyPayment_WithZeroRate_ReturnsAmountDividedByTerm()
    {
        var credit = Credit.Create(ValidClientId, 1_200_000m, 0m, 12);
        credit.MonthlyPayment().Should().Be(100_000m);
    }

    [Fact]
    public void ComputeMonthlyPayment_WithPositiveRate_ReturnsCorrectAmount()
    {
        var payment = Credit.ComputeMonthlyPayment(1_200_000m, 0m, 12);
        payment.Should().Be(100_000m);
    }

    [Fact]
    public void ComputeMonthlyPayment_WithRate_IsGreaterThanPrincipalOnly()
    {
        var withInterest = Credit.ComputeMonthlyPayment(1_200_000m, 0.18m, 12);
        var principalOnly = 1_200_000m / 12m;
        withInterest.Should().BeGreaterThan(principalOnly);
    }

    [Theory]
    [InlineData(CreditStatus.Rejected,    CreditStatus.Active)]
    [InlineData(CreditStatus.Rejected,    CreditStatus.Pending)]
    [InlineData(CreditStatus.Active,      CreditStatus.Pending)]
    [InlineData(CreditStatus.Active,      CreditStatus.Rejected)]
    [InlineData(CreditStatus.Closed,      CreditStatus.Active)]
    [InlineData(CreditStatus.Defaulted,   CreditStatus.Active)]
    public void UpdateStatus_IllegalTransition_Throws(CreditStatus from, CreditStatus to)
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);

        if (from != CreditStatus.Pending)
            credit.AssignRiskScore(15m, from switch
            {
                CreditStatus.Active      => Approved,
                CreditStatus.Rejected    => Rejected,
                CreditStatus.UnderReview => UnderReview,
                _                        => Approved
            });

        if (from == CreditStatus.Closed || from == CreditStatus.Defaulted)
            credit.UpdateStatus(from);

        var act = () => credit.UpdateStatus(to);
        act.Should().Throw<InvalidCreditOperationException>()
            .WithMessage("*Transición de estado no permitida*");
    }

    [Theory]
    [InlineData(CreditStatus.UnderReview)]
    [InlineData(CreditStatus.Active)]
    [InlineData(CreditStatus.Rejected)]
    public void UpdateStatus_LegalTransitionFromPending_Succeeds(CreditStatus to)
    {
        var credit = Credit.Create(ValidClientId, ValidAmount, ValidRate, ValidTerm);
        var act = () => credit.UpdateStatus(to);
        act.Should().NotThrow();
    }
}
