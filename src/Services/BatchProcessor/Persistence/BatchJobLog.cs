namespace Kriteriom.BatchProcessor.Persistence;

public class BatchJobLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Information";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
