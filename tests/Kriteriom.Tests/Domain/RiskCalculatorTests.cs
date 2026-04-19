using Kriteriom.Risk.Domain.Domain;

namespace Kriteriom.Tests.Domain;

public class RiskCalculatorTests
{
    private static readonly Guid CreditId = Guid.NewGuid();

    // ── Approved scenarios ────────────────────────────────────────────────────

    [Fact]
    public async Task Assess_LowDtiGoodScore_ReturnsApproved()
    {
        var result = await RiskCalculator.AssessAsync(
            creditId:            CreditId,
            amount:              1_000_000m,
            annualInterestRate:  0.10m,
            termMonths:          36,
            monthlyIncome:       5_000_000m,  // payment ≈ 32k → DTI ≈ 0.6% (well under 30%)
            existingMonthlyDebt: 0m,
            creditScore:         750);

        result.Decision.Should().Be("Approved");
        result.RiskScore.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(15);
    }

    // ── UnderReview scenarios ─────────────────────────────────────────────────

    [Fact]
    public async Task Assess_MediumDtiGoodScore_ReturnsUnderReview()
    {
        // amount=5M, rate=0.20, term=12 → payment≈459k; income=1M → newDTI≈46% (medium 30–60%)
        var result = await RiskCalculator.AssessAsync(
            creditId:            CreditId,
            amount:              5_000_000m,
            annualInterestRate:  0.20m,
            termMonths:          12,
            monthlyIncome:       1_000_000m,
            existingMonthlyDebt: 0m,
            creditScore:         700);

        result.Decision.Should().Be("UnderReview");
    }

    [Fact]
    public async Task Assess_FairCreditScore_ReturnsUnderReview()
    {
        var result = await RiskCalculator.AssessAsync(
            creditId:            CreditId,
            amount:              500_000m,
            annualInterestRate:  0.10m,
            termMonths:          36,
            monthlyIncome:       5_000_000m,
            existingMonthlyDebt: 0m,
            creditScore:         580);

        result.Decision.Should().Be("UnderReview");
    }

    // ── Rejected scenarios ────────────────────────────────────────────────────

    [Fact]
    public async Task Assess_HighDti_ReturnsRejected()
    {
        // Payment > 60% of income
        var result = await RiskCalculator.AssessAsync(
            creditId:            CreditId,
            amount:              10_000_000m,
            annualInterestRate:  0.25m,
            termMonths:          12,
            monthlyIncome:       1_000_000m,
            existingMonthlyDebt: 0m,
            creditScore:         750);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().BeGreaterThan(60);
    }

    [Fact]
    public async Task Assess_BadCreditScore_ReturnsRejected()
    {
        var result = await RiskCalculator.AssessAsync(
            creditId:            CreditId,
            amount:              500_000m,
            annualInterestRate:  0.10m,
            termMonths:          36,
            monthlyIncome:       5_000_000m,
            existingMonthlyDebt: 0m,
            creditScore:         450);

        result.Decision.Should().Be("Rejected");
    }

    [Fact]
    public async Task Assess_TotalDtiExceedsCap_ReturnsRejected()
    {
        // existing=500k + new≈125k (1.5M/0% rate/12mo) → total=625k/1M = 62.5% > 60%
        var result = await RiskCalculator.AssessAsync(
            creditId:            CreditId,
            amount:              1_500_000m,
            annualInterestRate:  0m,
            termMonths:          12,
            monthlyIncome:       1_000_000m,
            existingMonthlyDebt: 500_000m,
            creditScore:         750);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().BeGreaterThan(70m); // score = totalDti*1.1+20 → ≈88.75 for 62.5% DTI
    }

    // ── Input validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task Assess_ZeroIncome_ReturnsRejected()
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 1_000_000m, 0.10m, 36, monthlyIncome: 0m, 0m, 700);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().Be(99m);
    }

    [Fact]
    public async Task Assess_ZeroAmount_ReturnsRejected()
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 0m, 0.10m, 36, 5_000_000m, 0m, 700);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().Be(99m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task Assess_InvalidInterestRate_ReturnsRejected(decimal rate)
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 1_000_000m, rate, 36, 5_000_000m, 0m, 700);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().Be(99m);
    }

    [Theory]
    [InlineData(299)]
    [InlineData(851)]
    public async Task Assess_CreditScoreOutOfRange_ReturnsRejected(int score)
    {
        var result = await RiskCalculator.AssessAsync(
            CreditId, 1_000_000m, 0.10m, 36, 5_000_000m, 0m, score);

        result.Decision.Should().Be("Rejected");
        result.RiskScore.Should().Be(99m);
    }

    // ── Score bounds ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Assess_RiskScoreIsAlwaysBetween0And99()
    {
        var scenarios = new[]
        {
            (amount: 100_000m,     rate: 0.05m, months: 60, income: 10_000_000m, debt: 0m,         score: 800),
            (amount: 50_000_000m,  rate: 0.30m, months: 12, income: 1_000_000m,  debt: 800_000m,   score: 400),
            (amount: 1_000_000m,   rate: 0.15m, months: 36, income: 2_000_000m,  debt: 500_000m,   score: 600),
        };

        foreach (var s in scenarios)
        {
            var result = await RiskCalculator.AssessAsync(CreditId, s.amount, s.rate, s.months, s.income, s.debt, s.score);
            result.RiskScore.Should().BeInRange(0m, 99m);
        }
    }

    // ── Zero interest rate ────────────────────────────────────────────────────

    [Fact]
    public async Task Assess_ZeroInterestRate_ComputesCorrectly()
    {
        // 1_200_000 / 12 = 100_000/month; income = 2_000_000 → DTI = 5% → Approved
        var result = await RiskCalculator.AssessAsync(
            CreditId, 1_200_000m, 0m, 12, monthlyIncome: 2_000_000m, 0m, creditScore: 700);

        result.Decision.Should().Be("Approved");
    }
}
