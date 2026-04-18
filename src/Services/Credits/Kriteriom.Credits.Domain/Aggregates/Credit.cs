using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Events;
using Kriteriom.Credits.Domain.Exceptions;
using Kriteriom.SharedKernel.Domain;

namespace Kriteriom.Credits.Domain.Aggregates;

public class Credit : AggregateRoot
{
    private static readonly HashSet<(CreditStatus From, CreditStatus To)> AllowedTransitions =
    [
        (CreditStatus.Pending,     CreditStatus.UnderReview),
        (CreditStatus.Pending,     CreditStatus.Active),
        (CreditStatus.Pending,     CreditStatus.Rejected),
        (CreditStatus.UnderReview, CreditStatus.Active),
        (CreditStatus.UnderReview, CreditStatus.Rejected),
        (CreditStatus.Active,      CreditStatus.Closed),
        (CreditStatus.Active,      CreditStatus.Defaulted),
    ];

    public Guid ClientId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal InterestRate { get; private set; }
    public int TermMonths { get; private set; }
    public CreditStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public decimal? RiskScore { get; private set; }
    public string? RejectionReason { get; private set; }

    private Credit() { }

    public static Credit Create(Guid clientId, decimal amount, decimal interestRate, int termMonths = 36)
    {
        if (amount <= 0)
            throw new InvalidCreditOperationException("El monto debe ser mayor a cero");
        if (interestRate is < 0 or > 1)
            throw new InvalidCreditOperationException("La tasa de interés debe estar entre 0 y 1");
        if (clientId == Guid.Empty)
            throw new InvalidCreditOperationException("El identificador del cliente es inválido");
        if (termMonths is < 6 or > 120)
            throw new InvalidCreditOperationException("El plazo debe estar entre 6 y 120 meses");

        var credit = new Credit
        {
            ClientId     = clientId,
            Amount       = amount,
            InterestRate = interestRate,
            TermMonths   = termMonths,
            Status       = CreditStatus.Pending,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };

        credit.AddDomainEvent(new CreditCreatedDomainEvent(credit.Id, clientId, amount, interestRate));
        return credit;
    }

    public void UpdateStatus(CreditStatus newStatus, string? reason = null)
    {
        if (!AllowedTransitions.Contains((Status, newStatus)))
            throw new InvalidCreditOperationException(
                $"Transición de estado no permitida: {Status} → {newStatus}");

        var oldStatus = Status;
        Status          = newStatus;
        RejectionReason = reason;
        UpdatedAt       = DateTime.UtcNow;
        AddDomainEvent(new CreditStatusChangedDomainEvent(Id, oldStatus, newStatus));
    }

    public void AssignRiskScore(decimal score, RiskDecision decision)
    {
        if (score is < 0 or > 100)
            throw new InvalidCreditOperationException("El puntaje de riesgo debe estar entre 0 y 100");

        RiskScore = score;

        var newStatus = decision switch
        {
            RiskDecision.Approved    => CreditStatus.Active,
            RiskDecision.Rejected    => CreditStatus.Rejected,
            _                        => CreditStatus.UnderReview
        };

        UpdateStatus(newStatus);
    }

    public decimal MonthlyPayment() =>
        ComputeMonthlyPayment(Amount, InterestRate, TermMonths);

    public static decimal ComputeMonthlyPayment(decimal amount, decimal annualRate, int termMonths)
    {
        if (annualRate == 0) return amount / termMonths;
        var r = annualRate / 12m;
        return amount * r / (1m - (decimal)Math.Pow((double)(1m + r), -termMonths));
    }
}
