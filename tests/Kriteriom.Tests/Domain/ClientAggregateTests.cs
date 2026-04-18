using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Exceptions;

namespace Kriteriom.Tests.Domain;

public class ClientAggregateTests
{
    [Fact]
    public void Create_ValidParams_SetsCorrectFields()
    {
        var client = Client.Create("Juan Pérez", "juan@example.com", "123456789", 5_000_000m, EmploymentStatus.Employed);

        client.FullName.Should().Be("Juan Pérez");
        client.Email.Should().Be("juan@example.com");
        client.DocumentNumber.Should().Be("123456789");
        client.MonthlyIncome.Should().Be(5_000_000m);
        client.EmploymentStatus.Should().Be(EmploymentStatus.Employed);
        client.CreditScore.Should().BePositive().And.BeInRange(300, 850);
        client.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyFullName_Throws(string name)
    {
        var act = () => Client.Create(name, "x@x.com", "123", 1_000_000m, EmploymentStatus.Employed);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("notanemail")]
    [InlineData("   ")]
    public void Create_InvalidEmail_Throws(string email)
    {
        var act = () => Client.Create("Juan", email, "123", 1_000_000m, EmploymentStatus.Employed);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveIncome_Throws(decimal income)
    {
        var act = () => Client.Create("Juan", "j@x.com", "123", income, EmploymentStatus.Employed);
        act.Should().Throw<InvalidCreditOperationException>();
    }

    [Theory]
    [InlineData(15_000_000, EmploymentStatus.Employed, 850)]
    [InlineData(6_000_000,  EmploymentStatus.Employed, 780)]
    [InlineData(3_000_000,  EmploymentStatus.SelfEmployed, 660)]
    [InlineData(1_500_000,  EmploymentStatus.Retired, 550)]
    [InlineData(500_000,    EmploymentStatus.Unemployed, 400)]
    public void CalculateScore_KnownInputs_ReturnsExpected(decimal income, EmploymentStatus status, int expected)
    {
        var score = Client.CalculateScore(income, status);
        score.Should().Be(expected);
    }

    [Fact]
    public void CalculateScore_AlwaysBetween300And850()
    {
        foreach (var employment in Enum.GetValues<EmploymentStatus>())
        {
            var score = Client.CalculateScore(1m, employment);
            score.Should().BeInRange(300, 850);
        }
    }

    [Fact]
    public void ApplyDebtPenalty_DtiBelow20Pct_AppliesPenalty30()
    {
        var client = Client.Create("Test", "t@t.com", "1", 1_000_000m, EmploymentStatus.Employed);
        var initialScore = client.CreditScore;

        client.ApplyDebtPenalty(100_000m);

        client.CreditScore.Should().Be(initialScore - 30);
    }

    [Fact]
    public void ApplyDebtPenalty_Dti20To35Pct_AppliesPenalty55()
    {
        var client = Client.Create("Test", "t@t.com", "1", 1_000_000m, EmploymentStatus.Employed);
        var initialScore = client.CreditScore;

        client.ApplyDebtPenalty(250_000m);

        client.CreditScore.Should().Be(initialScore - 55);
    }

    [Fact]
    public void ApplyDebtPenalty_Dti35To50Pct_AppliesPenalty80()
    {
        var client = Client.Create("Test", "t@t.com", "1", 1_000_000m, EmploymentStatus.Employed);
        var initialScore = client.CreditScore;

        client.ApplyDebtPenalty(400_000m);

        client.CreditScore.Should().Be(initialScore - 80);
    }

    [Fact]
    public void ApplyDebtPenalty_DtiAbove50Pct_AppliesPenalty110()
    {
        var client = Client.Create("Test", "t@t.com", "1", 1_000_000m, EmploymentStatus.Employed);
        var initialScore = client.CreditScore;

        client.ApplyDebtPenalty(550_000m);

        client.CreditScore.Should().Be(initialScore - 110);
    }

    [Fact]
    public void ApplyDebtPenalty_ScoreNeverFallsBelow300()
    {
        var client = Client.Create("Test", "t@t.com", "1", 500_000m, EmploymentStatus.Unemployed);

        client.ApplyDebtPenalty(400_000m);

        client.CreditScore.Should().BeGreaterThanOrEqualTo(300);
    }

    [Fact]
    public void ApplyDebtPenalty_UsesAggregateOwnIncome()
    {
        var client = Client.Create("Test", "t@t.com", "1", 1_000_000m, EmploymentStatus.Employed);
        var initialScore = client.CreditScore;

        client.ApplyDebtPenalty(100_000m);

        client.CreditScore.Should().Be(initialScore - 30);
    }

    [Fact]
    public void Update_ChangesFieldsAndRecalculatesScore()
    {
        var client = Client.Create("Old Name", "x@x.com", "1", 1_000_000m, EmploymentStatus.Unemployed);
        var oldScore = client.CreditScore;

        client.Update("New Name", 10_000_000m, EmploymentStatus.Employed);

        client.FullName.Should().Be("New Name");
        client.MonthlyIncome.Should().Be(10_000_000m);
        client.CreditScore.Should().BeGreaterThan(oldScore);
    }
}
