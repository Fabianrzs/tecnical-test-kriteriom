using Hangfire;
using Kriteriom.BatchProcessor.Jobs;
using Kriteriom.BatchProcessor.Persistence;
using Kriteriom.BatchProcessor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kriteriom.BatchProcessor.Controllers;

[ApiController]
[Route("api/batch")]
[Produces("application/json")]
public class BatchController(
    IBackgroundJobClient backgroundJobClient,
    IBatchStatusService batchStatusService,
    IConfiguration configuration,
    ILogger<BatchController> logger)
    : ControllerBase
{
    /// <summary>
    /// Enqueues the credit status recalculation batch job.
    /// </summary>
    [HttpPost("recalculate")]
    [ProducesResponseType(typeof(EnqueuedJobResponse), StatusCodes.Status202Accepted)]
    public IActionResult Recalculate()
    {
        var jobId = backgroundJobClient.Enqueue<CreditStatusRecalculationJob>(
            queue: "batch",
            methodCall: job => job.ExecuteAsync(CancellationToken.None));

        logger.LogInformation("Enqueued CreditStatusRecalculationJob with Hangfire JobId={JobId}", jobId);

        return Accepted(new EnqueuedJobResponse(jobId, "CreditStatusRecalculation", "Enqueued"));
    }

    /// <summary>
    /// Returns the current status of all batch job checkpoints.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(IEnumerable<BatchJobCheckpoint>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var checkpoints = await batchStatusService.GetCheckpointsAsync(ct);

        return Ok(checkpoints);
    }

    /// <summary>
    /// Enqueues the test data seeding job (only when FeatureFlags:SeedTestData is enabled).
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(typeof(EnqueuedJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult Seed()
    {
        bool seedEnabled = configuration.GetValue<bool>("FeatureFlags:SeedTestData", false);
        if (!seedEnabled)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Feature Disabled",
                Status = StatusCodes.Status403Forbidden,
                Detail = "FeatureFlags:SeedTestData is not enabled in configuration."
            });
        }

        var jobId = backgroundJobClient.Enqueue<SeedTestDataJob>(
            queue: "seed",
            methodCall: job => job.ExecuteAsync(CancellationToken.None));

        logger.LogInformation("Enqueued SeedTestDataJob with Hangfire JobId={JobId}", jobId);

        return Accepted(new EnqueuedJobResponse(jobId, "SeedTestData", "Enqueued"));
    }
}

public record EnqueuedJobResponse(string JobId, string JobName, string Status);
