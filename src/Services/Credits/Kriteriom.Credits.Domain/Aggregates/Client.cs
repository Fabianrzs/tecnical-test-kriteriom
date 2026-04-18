using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.SharedKernel.Domain;

namespace Kriteriom.Credits.Domain.Aggregates;

public class Client : AggregateRoot
{
    private const int BasePoints = 300;

    public string FullName        { get; private set; } = string.Empty;
    public string Email           { get; private set; } = string.Empty;
    public string DocumentNumber  { get; private set; } = string.Empty;
    public decimal MonthlyIncome  { get; private set; }
    public int CreditScore        { get; private set; }
    public EmploymentStatus EmploymentStatus { get; private set; }
    public DateTime CreatedAt     { get; private set; }
    public DateTime UpdatedAt     { get; private set; }

    private Client() { }

    public static Client Create(
        string fullName,
        string email,
        string documentNumber,
        decimal monthlyIncome,
        EmploymentStatus employmentStatus)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidCreditOperationException("FullName is required");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new InvalidCreditOperationException("A valid Email is required");
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new InvalidCreditOperationException("DocumentNumber is required");
        if (monthlyIncome <= 0)
            throw new InvalidCreditOperationException("MonthlyIncome must be greater than zero");

        var client = new Client
        {
            FullName         = fullName.Trim(),
            Email            = email.Trim().ToLowerInvariant(),
            DocumentNumber   = documentNumber.Trim(),
            MonthlyIncome    = monthlyIncome,
            EmploymentStatus = employmentStatus,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
            CreditScore = CalculateScore(monthlyIncome, employmentStatus)
        };

        return client;
    }

    public void Update(string fullName, decimal monthlyIncome, EmploymentStatus employmentStatus)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidCreditOperationException("FullName is required");
        if (monthlyIncome <= 0)
            throw new InvalidCreditOperationException("MonthlyIncome must be greater than zero");

        FullName         = fullName.Trim();
        MonthlyIncome    = monthlyIncome;
        EmploymentStatus = employmentStatus;
        CreditScore      = CalculateScore(monthlyIncome, employmentStatus);
        UpdatedAt        = DateTime.UtcNow;
    }

    /// <summary>
    /// Internal scoring model (300–850 scale, same as Equifax/TransUnion).
    ///
    /// Income tier  (max 350 pts):
    ///   > 10 M/mo  → 350
    ///   5–10 M/mo  → 280
    ///   2–5  M/mo  → 200
    ///   1–2  M/mo  → 120
    ///   &lt; 1  M/mo  → 50
    ///
    /// Employment   (max 200 pts):
    ///   Employed     → 200
    ///   SelfEmployed → 160
    ///   Retired      → 130
    ///   Unemployed   → 50
    ///
    /// Base = 300  →  max total = 300 + 350 + 200 = 850
    /// </summary>
    /// <summary>
    /// Reduces CreditScore when a credit is approved. Each approved credit increases
    /// debt load, which reduces creditworthiness proportionally to its DTI contribution.
    /// </summary>
    public void ApplyDebtPenalty(decimal monthlyPayment)
    {
        if (MonthlyIncome <= 0) return;
        var dti = monthlyPayment / MonthlyIncome * 100m;

        var penalty = dti switch
        {
            < 20m  => 30,
            < 35m  => 55,
            < 50m  => 80,
            _      => 110
        };

        CreditScore = Math.Max(BasePoints, CreditScore - penalty);
        UpdatedAt   = DateTime.UtcNow;
    }

    public static int CalculateScore(decimal monthlyIncome, EmploymentStatus employment)
    {
        var incomePoints = monthlyIncome switch
        {
            > 10_000_000m => 350,
            > 5_000_000m  => 280,
            > 2_000_000m  => 200,
            > 1_000_000m  => 120,
            _             => 50
        };

        var employmentPoints = employment switch
        {
            EmploymentStatus.Employed     => 200,
            EmploymentStatus.SelfEmployed => 160,
            EmploymentStatus.Retired      => 130,
            _                             => 50
        };

        return BasePoints + incomePoints + employmentPoints;
    }
}
