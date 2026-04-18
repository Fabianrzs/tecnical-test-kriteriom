namespace Kriteriom.SharedKernel.Messaging;

public record CreditRiskEvaluationRequestedEvent : IntegrationEvent
{
    public Guid    CreditId            { get; init; }
    public Guid    ClientId            { get; init; }
    public decimal Amount              { get; init; }
    public string  Status              { get; init; } = string.Empty;
    public decimal InterestRate        { get; init; }
    public int     TermMonths          { get; init; }
    public decimal MonthlyIncome       { get; init; }
    public decimal ExistingMonthlyDebt { get; init; }
    public int     ClientCreditScore   { get; init; }
}
