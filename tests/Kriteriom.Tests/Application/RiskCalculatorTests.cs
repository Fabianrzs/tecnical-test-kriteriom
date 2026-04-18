using Kriteriom.Risk.Domain.Domain;

namespace Kriteriom.Tests.Application;

public class RiskCalculatorTests
{
    private static readonly Guid CreditId = Guid.NewGuid();

    [Fact]
    public async Task Assess_ZeroIncome_ReturnsRejectedScore99()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 5_000_000m, 0.18m, 36, 0m, 0m, 700);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().Be(99m);
        result.Reason.Should().Contain("Ingreso");
    }

    [Fact]
    public async Task Assess_ZeroAmount_ReturnsRejectedScore99()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 0m, 0.18m, 36, 2_000_000m, 0m, 700);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().Be(99m);
    }

    [Fact]
    public async Task Assess_LowDtiGoodScore_ReturnsApproved()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 1_200_000m, 0m, 12, 1_000_000m, 0m, 700);

        result.Decision.Should().Be("Approved");
        result.RiskScore.Should().BeInRange(0m, 20m);
    }

    [Fact]
    public async Task Assess_ZeroInterestRate_UsesAmountDividedByTerm()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 600_000m, 0m, 12, 1_000_000m, 0m, 700);

        result.Decision.Should().Be("Approved");
    }

    [Fact]
    public async Task Assess_MediumDti_ReturnsUnderReview()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 1_200_000m, 0m, 12, 300_000m, 0m, 700);

        result.Decision.Should().Be("UnderReview");
    }

    [Fact]
    public async Task Assess_FairScore_ReturnsUnderReview()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 1_200_000m, 0m, 12, 1_000_000m, 0m, 580);

        result.Decision.Should().Be("UnderReview");
    }

    [Fact]
    public async Task Assess_CreditScore649_ReturnsUnderReview()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 1_200_000m, 0m, 12, 1_000_000m, 0m, 649);

        result.Decision.Should().Be("UnderReview");
    }

    [Fact]
    public async Task Assess_HighNewDti_ReturnsRejected()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 1_200_000m, 0m, 12, 150_000m, 0m, 700);

        result.Decision.Should().Be("Rejected");
        result.Reason.Should().Contain("60%");
    }

    [Fact]
    public async Task Assess_BadCreditScore_ReturnsRejected()
    {
        var result = await RiskCalculator.AssessAsync(CreditId, 1_200_000m, 0m, 12, 1_000_000m, 0m, 450);

        result.Decision.Should().Be("Rejected");
        result.Reason.Should().ContainEquivalentOf("puntaje");
    }

    [Fact]
    public async Task Assess_TotalDtiExceeds60Pct_HardCapRejectsEvenWithLowNewDti()
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 1_200_000m, 0m, 12,
            monthlyIncome: 900_000m,
            existingMonthlyDebt: 500_000m,
            creditScore: 750);

        result.Decision.Should().Be("Rejected");
        result.Reason.Should().Contain("60%");
    }

    [Fact]
    public async Task Assess_ExistingDebtDoesNotTriggerHardCap_ProceedsNormally()
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 1_200_000m, 0m, 12,
            monthlyIncome: 2_000_000m,
            existingMonthlyDebt: 100_000m,
            creditScore: 750);

        result.Decision.Should().Be("Approved");
    }

    [Theory]
    [InlineData(0.18, 36)]
    [InlineData(0.12, 60)]
    [InlineData(0.24, 12)]
    public async Task Assess_RiskScore_AlwaysBetween0And99(decimal rate, int term)
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 5_000_000m, rate, term, 2_000_000m, 0m, 650);

        result.RiskScore.Should().BeInRange(0m, 99m);
    }

    [Fact]
    public async Task Assess_CreditIdIsPreserved()
    {
        var id = Guid.NewGuid();
        var result = await RiskCalculator.AssessAsync(id, 1_200_000m, 0m, 12, 1_000_000m, 0m, 700);
        result.CreditId.Should().Be(id);
    }
}
