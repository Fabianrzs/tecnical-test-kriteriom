namespace Kriteriom.BatchProcessor.Persistence;

public class BatchJobCheckpoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobName { get; set; } = string.Empty;
    public int LastProcessedOffset { get; set; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }

    /// <summary>Possible values: Running, Paused, Completed, Failed</summary>
    public string Status { get; set; } = "Running";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}
