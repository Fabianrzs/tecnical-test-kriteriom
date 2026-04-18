namespace Kriteriom.Credits.Application.DTOs;

public record CreditStatsDto(
    int TotalCredits,
    int TotalClients,
    double ApprovalRate,
    int Pending,
    int UnderReview,
    int Approved,
    int Rejected,
    int Closed,
    int Defaulted);
