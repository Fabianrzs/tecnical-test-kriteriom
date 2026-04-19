namespace Kriteriom.SharedKernel.Infrastructure.Persistence;

public class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
