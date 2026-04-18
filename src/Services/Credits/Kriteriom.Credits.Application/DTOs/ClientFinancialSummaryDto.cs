namespace Kriteriom.Credits.Application.DTOs;

public record ClientFinancialSummaryDto(
    decimal ExistingMonthlyDebt,
    int     ActiveCreditsCount);
