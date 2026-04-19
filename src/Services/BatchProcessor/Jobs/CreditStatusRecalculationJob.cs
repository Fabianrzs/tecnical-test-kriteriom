using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Kriteriom.BatchProcessor.Persistence;
using Kriteriom.SharedKernel.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.BatchProcessor.Jobs;

/// <summary>
/// Batch job that paginates through all credits via the Credits API, evaluates recalculation
/// rules, and updates statuses in parallel. Supports checkpoint/resume for processing 1M+ records.
/// </summary>
public class CreditStatusRecalculationJob(
    ILogger<CreditStatusRecalculationJob> logger,
    BatchDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IBus bus,
    IConfiguration configuration)
{
    private const string JobName = "CreditStatusRecalculation";

    [DisableConcurrentExecution(timeoutInSeconds: 0)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        int batchSize = configuration.GetValue<int>("BatchProcessing:BatchSize", 1000);
        int degreeOfParallelism = configuration.GetValue<int>("BatchProcessing:DegreeOfParallelism", 4);
        int maxDbConcurrency = configuration.GetValue<int>("BatchProcessing:MaxDbConcurrency", 4);

        logger.LogInformation(
            "Starting {JobName} — BatchSize={BatchSize}, DOP={DOP}",
            JobName, batchSize, degreeOfParallelism);

        // ─── Step 1: create or resume checkpoint ───────────────────────────
        var checkpoint = await dbContext.BatchJobCheckpoints
            .Where(c => c.JobName == JobName && c.Status == "Running")
            .OrderByDescending(c => c.StartedAt)
            .FirstOrDefaultAsync(ct);

        bool isResume = checkpoint is not null;

        if (!isResume)
        {
            checkpoint = new BatchJobCheckpoint
            {
                JobName = JobName,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.BatchJobCheckpoints.Add(checkpoint);
            await dbContext.SaveChangesAsync(ct);
        }
        else
        {
            logger.LogInformation(
                "Resuming {JobName} from offset {Offset} (previously processed {Done} records)",
                JobName, checkpoint!.LastProcessedOffset, checkpoint.ProcessedRecords);
        }

        var httpClient = httpClientFactory.CreateClient("credits-api");

        // ─── Step 2: get total credits count ───────────────────────────────
        int totalRecords = checkpoint.TotalRecords;
        if (totalRecords == 0)
        {
            var countResponse = await httpClient.GetFromJsonAsync<PagedResultEnvelope<CreditApiDto>>(
                $"/api/credits?page=1&pageSize=1", ct);

            totalRecords = countResponse?.TotalCount ?? 0;

            if (totalRecords == 0)
            {
                logger.LogWarning("No credits found. Marking job as Completed.");
                checkpoint.Status = "Completed";
                checkpoint.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            checkpoint.TotalRecords = totalRecords;
            await dbContext.SaveChangesAsync(ct);
        }

        logger.LogInformation("{JobName}: {Total} credits to process, starting at offset {Offset}",
            JobName, totalRecords, checkpoint.LastProcessedOffset);

        // ─── Step 3 & 4: paginate and process in batches ───────────────────
        int startPage = (checkpoint.LastProcessedOffset / batchSize) + 1;
        int batchNumber = startPage;

        // Semaphore limits concurrent DB writes to avoid saturation
        using var dbSemaphore = new SemaphoreSlim(maxDbConcurrency, maxDbConcurrency);

        try
        {
            for (int page = startPage; ; page++)
            {
                ct.ThrowIfCancellationRequested();

                var pageResult = await httpClient.GetFromJsonAsync<PagedResultEnvelope<CreditApiDto>>(
                    $"/api/credits?page={page}&pageSize={batchSize}", ct);

                if (pageResult is null || !pageResult.Items.Any())
                    break;

                var batchCredits = pageResult.Items.ToList();
                int updatedInBatch = 0;
                var batchErrors = new ConcurrentBag<string>();

                // ─── Step 4: Parallel.ForEachAsync with bounded DOP ────────
                await Parallel.ForEachAsync(
                    batchCredits,
                    new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism, CancellationToken = ct },
                    async (credit, innerCt) =>
                    {
                        try
                        {
                            // Re-trigger risk evaluation for Pending credits with no risk score
                            if (credit.Status == "Pending" && !credit.RiskScore.HasValue)
                            {
                                await TriggerRiskEvaluationAsync(credit, httpClient, bus, logger, innerCt);
                                return;
                            }

                            var (newStatus, reason) = DetermineNewStatus(credit);
                            if (newStatus is null)
                                return;

                            // ─── Step 6: update status via Credits API ─────
                            var updatePayload = JsonSerializer.Serialize(new
                            {
                                NewStatus = MapStatusToInt(newStatus),
                                Reason = reason
                            });

                            using var content = new StringContent(updatePayload, Encoding.UTF8, "application/json");
                            var updateResponse = await httpClient.PutAsync(
                                $"/api/credits/{credit.Id}/status", content, innerCt);

                            if (!updateResponse.IsSuccessStatusCode)
                            {
                                logger.LogWarning(
                                    "Failed to update credit {CreditId} to {Status}: HTTP {Code}",
                                    credit.Id, newStatus, (int)updateResponse.StatusCode);
                                return;
                            }

                            // Publish integration event for status change
                            await bus.Publish(new CreditUpdatedIntegrationEvent
                            {
                                CreditId = credit.Id,
                                OldStatus = credit.Status,
                                NewStatus = newStatus,
                                UpdatedAt = DateTime.UtcNow
                            }, innerCt);

                            await dbSemaphore.WaitAsync(innerCt);
                            try
                            {
                                dbContext.BatchJobLogs.Add(new BatchJobLog
                                {
                                    JobName = JobName,
                                    Message = $"Credit {credit.Id}: {credit.Status} → {newStatus} ({reason})",
                                    Level = "Information",
                                    Timestamp = DateTime.UtcNow
                                });
                                // Avoid individual SaveChanges per credit — batch them
                            }
                            finally
                            {
                                dbSemaphore.Release();
                            }

                            Interlocked.Increment(ref updatedInBatch);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogError(ex, "Error processing credit {CreditId}", credit.Id);
                            batchErrors.Add($"Credit {credit.Id}: {ex.Message}");

                            await dbSemaphore.WaitAsync(innerCt);
                            try
                            {
                                dbContext.BatchJobLogs.Add(new BatchJobLog
                                {
                                    JobName = JobName,
                                    Message = $"Credit {credit.Id}: failed — {ex.Message}",
                                    Level = "Error",
                                    Timestamp = DateTime.UtcNow
                                });
                            }
                            finally
                            {
                                dbSemaphore.Release();
                            }
                        }
                    });

                // ─── Step 7: update checkpoint after each batch ────────────
                checkpoint.LastProcessedOffset += batchCredits.Count;
                checkpoint.ProcessedRecords += batchCredits.Count;
                checkpoint.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(ct); // flush batch logs + checkpoint in one round-trip

                // ─── Step 8: progress log ──────────────────────────────────
                if (!batchErrors.IsEmpty)
                    logger.LogWarning(
                        "Batch {BatchNumber}: {ErrorCount} error(s). First errors: {Errors}",
                        batchNumber, batchErrors.Count, string.Join("; ", batchErrors.Take(5)));

                logger.LogInformation(
                    "Batch {BatchNumber}: Processed {Processed}/{Total} credits ({Updated} updated, {Errors} errors)",
                    batchNumber, checkpoint.ProcessedRecords, totalRecords, updatedInBatch, batchErrors.Count);

                batchNumber++;

                // Stop when we have consumed all pages
                if (!pageResult.HasNextPage)
                    break;
            }

            // ─── Step 10: mark as Completed ────────────────────────────────
            checkpoint.Status = "Completed";
            checkpoint.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "{JobName} completed. Processed {Total} credits.",
                JobName, checkpoint.ProcessedRecords);
        }
        catch (OperationCanceledException)
        {
            // ─── Step 9: paused via cancellation (Hangfire retry support) ──
            checkpoint.Status = "Paused";
            checkpoint.UpdatedAt = DateTime.UtcNow;
            checkpoint.ErrorMessage = "Job was cancelled / paused.";
            await dbContext.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("{JobName} was cancelled at offset {Offset}.", JobName, checkpoint.LastProcessedOffset);
            throw;
        }
        catch (Exception ex)
        {
            // ─── Step 9: failure path — save checkpoint so Hangfire can retry
            checkpoint.Status = "Failed";
            checkpoint.UpdatedAt = DateTime.UtcNow;
            checkpoint.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(CancellationToken.None);

            logger.LogError(ex,
                "{JobName} failed at offset {Offset}. Checkpoint saved for resume.",
                JobName, checkpoint.LastProcessedOffset);
            throw;
        }
    }

    // ─── Recalculation rules ────────────────────────────────────────────────

    /// <summary>
    /// Returns (newStatus, reason) when a status change is required, or (null, null) to keep current.
    ///
    /// Thresholds aligned with DTI-based risk model:
    ///   Approved score  ≈ 0–14  (DTI × 0.5, where DTI &lt; 30%)
    ///   Review   score  ≈ 39–63 (DTI × 0.8 + 15)
    ///   Rejected score  ≈ 70–99
    /// </summary>
    private static (string? newStatus, string? reason) DetermineNewStatus(CreditApiDto credit)
    {
        // Rule 1: Active credits whose DTI-based score exceeds 30 — they slipped through
        // or conditions changed (client income dropped). Flag for manual review.
        if (credit.Status == "Active" && credit.RiskScore.HasValue && credit.RiskScore.Value > 30)
            return ("UnderReview", $"Risk score {credit.RiskScore:F1} exceeds active-credit threshold — manual review required");

        // Rule 2: Pending credits older than 7 days → Rejected (expired, no risk assessment received)
        if (credit.Status == "Pending" && credit.CreatedAt < DateTime.UtcNow.AddDays(-7))
            return ("Rejected", "Credit request expired — no risk assessment received within 7 days");

        // Rule 3: UnderReview credits older than 30 days → Rejected (manual review timed out)
        if (credit.Status == "UnderReview" && credit.UpdatedAt < DateTime.UtcNow.AddDays(-30))
            return ("Rejected", "Manual review timed out — credit under review for more than 30 days");

        return (null, null);
    }

    /// <summary>Maps status string to integer for the Credits API request body.</summary>
    private static int MapStatusToInt(string status) => status switch
    {
        "Pending" => 0,
        "UnderReview" => 1,
        "Active" => 2,
        "Rejected" => 3,
        "Closed" => 4,
        "Defaulted" => 5,
        _ => 0
    };

    private static async Task TriggerRiskEvaluationAsync(
        CreditApiDto credit,
        HttpClient httpClient,
        IBus bus,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var client = await httpClient.GetFromJsonAsync<ClientApiDto>(
                $"/api/clients/{credit.ClientId}", ct);

            if (client is null)
            {
                logger.LogWarning("Client {ClientId} not found for credit {CreditId}", credit.ClientId, credit.Id);
                return;
            }

            var financial = await httpClient.GetFromJsonAsync<ClientFinancialDto>(
                $"/api/clients/{credit.ClientId}/financial-summary", ct);

            await bus.Publish(new CreditRiskEvaluationRequestedEvent
            {
                CreditId            = credit.Id,
                ClientId            = credit.ClientId,
                Amount              = credit.Amount,
                Status              = credit.Status,
                InterestRate        = credit.InterestRate,
                TermMonths          = credit.TermMonths,
                MonthlyIncome       = client.MonthlyIncome,
                ExistingMonthlyDebt = financial?.ExistingMonthlyDebt ?? 0,
                ClientCreditScore   = client.CreditScore
            }, ct);

            logger.LogInformation(
                "Re-triggered risk evaluation for credit {CreditId} (client={ClientId}, score={Score})",
                credit.Id, credit.ClientId, client.CreditScore);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to trigger risk evaluation for credit {CreditId}", credit.Id);
        }
    }
}

// ─── Local DTOs for Credits API responses ───────────────────────────────────

internal class PagedResultEnvelope<T>
{
    [JsonPropertyName("items")]
    public IEnumerable<T> Items { get; init; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; init; }
}

internal class CreditApiDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("clientId")]
    public Guid ClientId { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("interestRate")]
    public decimal InterestRate { get; init; }

    [JsonPropertyName("termMonths")]
    public int TermMonths { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("riskScore")]
    public decimal? RiskScore { get; init; }

    [JsonPropertyName("isHighRisk")]
    public bool IsHighRisk { get; init; }
}

internal class ClientApiDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("monthlyIncome")]
    public decimal MonthlyIncome { get; init; }

    [JsonPropertyName("creditScore")]
    public int CreditScore { get; init; }
}

internal class ClientFinancialDto
{
    [JsonPropertyName("existingMonthlyDebt")]
    public decimal ExistingMonthlyDebt { get; init; }
}
