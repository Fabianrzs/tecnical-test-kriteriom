using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Domain.Aggregates;

namespace Kriteriom.Credits.Application.Mapping;

public static class ClientMappingExtensions
{
    public static ClientDto ToDto(this Client client) => new(
        client.Id,
        client.FullName,
        client.Email,
        client.DocumentNumber,
        client.MonthlyIncome,
        client.CreditScore,
        client.EmploymentStatus.ToString(),
        client.CreatedAt,
        client.UpdatedAt);
}
