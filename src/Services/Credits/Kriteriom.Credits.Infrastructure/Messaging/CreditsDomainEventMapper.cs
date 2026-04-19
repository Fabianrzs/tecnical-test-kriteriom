using Kriteriom.Credits.Domain.Events;
using Kriteriom.SharedKernel.Application.Services;
using Kriteriom.SharedKernel.Domain;
using Kriteriom.SharedKernel.Messaging;

namespace Kriteriom.Credits.Infrastructure.Messaging;

public class CreditsDomainEventMapper : IDomainEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        CreditCreatedDomainEvent e => new CreditCreatedIntegrationEvent
        {
            CreditId            = e.CreditId,
            ClientId            = e.ClientId,
            Amount              = e.Amount,
            Status              = "Pending",
            InterestRate        = e.InterestRate,
            TermMonths          = e.TermMonths,
            MonthlyIncome       = e.MonthlyIncome,
            ExistingMonthlyDebt = e.ExistingMonthlyDebt,
            ClientCreditScore   = e.ClientCreditScore
        },
        CreditStatusChangedDomainEvent e => new CreditUpdatedIntegrationEvent
        {
            CreditId  = e.CreditId,
            OldStatus = e.OldStatus.ToString(),
            NewStatus = e.NewStatus.ToString(),
            UpdatedAt = e.UpdatedAt
        },
        ClientCreatedDomainEvent e => new ClientCreatedIntegrationEvent
        {
            ClientId         = e.ClientId,
            FullName         = e.FullName,
            Email            = e.Email,
            DocumentNumber   = e.DocumentNumber,
            MonthlyIncome    = e.MonthlyIncome,
            EmploymentStatus = e.EmploymentStatus
        },
        ClientUpdatedDomainEvent e => new ClientUpdatedIntegrationEvent
        {
            ClientId         = e.ClientId,
            FullName         = e.FullName,
            MonthlyIncome    = e.MonthlyIncome,
            EmploymentStatus = e.EmploymentStatus
        },
        _ => null
    };
}
