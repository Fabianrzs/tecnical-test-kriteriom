namespace Kriteriom.Risk.Domain.Domain;

public static class RiskCalculator
{
    private const decimal MaxTotalDtiPercent  = 60m;
    private const decimal MaxNewDtiPercent    = 60m;
    private const decimal MediumDtiThreshold  = 30m;
    private const int     GoodCreditScore     = 650;
    private const int     FairCreditScoreMin  = 500;
    /// <summary>
    /// Evaluates credit risk using:
    ///   - New credit DTI (this payment / income)
    ///   - Total DTI (existing debt + new payment) / income — must not exceed 60%
    ///   - Client credit score (300–850)
    ///
    /// Decision matrix:
    ///   totalDti &gt; 60%                          → Rejected  (debt capacity exceeded)
    ///   newDti &lt; 30% AND creditScore &gt;= 650      → Approved
    ///   newDti 30–60% OR creditScore 500–649      → UnderReview
    ///   newDti &gt; 60% OR creditScore &lt; 500         → Rejected
    /// </summary>
    public static Task<RiskAssessmentResult> AssessAsync(
        Guid    creditId,
        decimal amount,
        decimal annualInterestRate,
        int     termMonths,
        decimal monthlyIncome,
        decimal existingMonthlyDebt,
        int     creditScore,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (monthlyIncome <= 0)
            return Task.FromResult(new RiskAssessmentResult(
                creditId, 99m, "Rejected", "Ingreso mensual no registrado"));

        if (amount <= 0)
            return Task.FromResult(new RiskAssessmentResult(
                creditId, 99m, "Rejected", "Monto de crédito inválido"));

        if (creditScore is < 300 or > 850)
            return Task.FromResult(new RiskAssessmentResult(
                creditId, 99m, "Rejected", $"Puntaje de crédito fuera de rango ({creditScore})"));

        if (annualInterestRate is < 0 or > 1)
            return Task.FromResult(new RiskAssessmentResult(
                creditId, 99m, "Rejected", $"Tasa de interés anual inválida ({annualInterestRate:P2}). Debe estar entre 0 y 1 (0%–100%)."));

        var monthlyRate = annualInterestRate / 12m;
        var newPayment = monthlyRate == 0
            ? amount / termMonths
            : amount * monthlyRate / (1m - (decimal)Math.Pow((double)(1m + monthlyRate), -termMonths));

        var newDti   = newPayment / monthlyIncome * 100m;
        var totalDti = (existingMonthlyDebt + newPayment) / monthlyIncome * 100m;

        // Hard cap: accumulated debt load must not exceed MaxTotalDtiPercent of income
        if (totalDti > MaxTotalDtiPercent)
        {
            var score = Math.Min(Math.Round(totalDti * 1.1m + 20m, 2), 99m);
            return Task.FromResult(new RiskAssessmentResult(
                creditId, score, "Rejected",
                $"Carga total de deuda ({totalDti:F1}%) supera el límite del 60% de los ingresos mensuales"));
        }

        var (riskScore, decision, reason) = DetermineOutcome(newDti, totalDti, creditScore);

        return Task.FromResult(new RiskAssessmentResult(
            CreditId:  creditId,
            RiskScore: Math.Round(riskScore, 2),
            Decision:  decision,
            Reason:    reason));
    }

    private static (decimal score, string decision, string reason) DetermineOutcome(
        decimal newDti, decimal totalDti, int creditScore)
    {
        var highDti   = newDti > MaxNewDtiPercent;
        var mediumDti = newDti is >= MediumDtiThreshold and <= MaxNewDtiPercent;

        bool goodScore = creditScore >= GoodCreditScore;
        bool fairScore = creditScore is >= FairCreditScoreMin and < GoodCreditScore;
        bool badScore  = creditScore < FairCreditScoreMin;

        if (highDti || badScore)
        {
            var score = Math.Min(Math.Round(newDti * 1.1m + 25m, 2), 99m);
            var reason = highDti && badScore
                ? $"DTI alto ({newDti:F1}%) y puntaje de crédito bajo ({creditScore})"
                : highDti
                    ? $"DTI demasiado alto ({newDti:F1}% — límite 60%)"
                    : $"Puntaje de crédito insuficiente ({creditScore} — mínimo 500)";
            return (score, "Rejected", reason);
        }

        if (mediumDti || fairScore)
        {
            var score = Math.Round(newDti * 0.8m + 15m, 2);
            var reason = mediumDti
                ? $"DTI moderado ({newDti:F1}%), requiere revisión manual"
                : $"Puntaje de crédito regular ({creditScore}), requiere revisión";
            return (score, "UnderReview", reason);
        }

        var approvedScore = Math.Round(newDti * 0.5m, 2);
        return (approvedScore, "Approved",
            $"DTI {newDti:F1}% dentro del límite, puntaje {creditScore}, carga total {totalDti:F1}%");
    }
}
