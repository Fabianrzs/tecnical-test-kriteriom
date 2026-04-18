namespace Kriteriom.Credits.Application.DTOs;

public record ClientDto(
    Guid   Id,
    string FullName,
    string Email,
    string DocumentNumber,
    decimal MonthlyIncome,
    int     CreditScore,
    string  EmploymentStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt);
