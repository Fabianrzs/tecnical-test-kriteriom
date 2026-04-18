namespace Kriteriom.Risk.Domain.Domain;

public static class RiskCalculator
{
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

        var monthlyRate = annualInterestRate / 12m;
        decimal newPayment = monthlyRate == 0
            ? amount / termMonths
            : amount * monthlyRate / (1m - (decimal)Math.Pow((double)(1m + monthlyRate), -termMonths));

        var newDti   = newPayment / monthlyIncome * 100m;
        var totalDti = (existingMonthlyDebt + newPayment) / monthlyIncome * 100m;

        // Hard cap: accumulated debt load must not exceed 60% of income
        if (totalDti > 60m)
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
        bool highDti   = newDti > 60m;
        bool mediumDti = newDti is >= 30m and <= 60m;
        bool lowDti    = newDti < 30m;

        bool goodScore = creditScore >= 650;
        bool fairScore = creditScore is >= 500 and < 650;
        bool badScore  = creditScore < 500;

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
