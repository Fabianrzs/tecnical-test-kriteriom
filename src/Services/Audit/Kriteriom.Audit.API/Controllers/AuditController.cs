using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Kriteriom.Audit.API.Controllers;

[ApiController]
[Route("api/audit")]
[Produces("application/json")]
public class AuditController(
    IAuditRepository auditRepository,
    ILogger<AuditController> logger)
    : ControllerBase
{
    /// <summary>
    /// Returns paginated recent audit records across all credits (Admin only).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, total) = await auditRepository.GetRecentAsync(page, pageSize, ct);
        return Ok(new
        {
            items,
            totalCount = total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Returns all audit records for a given credit, ordered by OccurredOn descending.
    /// </summary>
    /// <param name="creditId">Credit identifier</param>
    /// <param name="limit">Maximum number of records to return (default 100)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("credit/{creditId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AuditRecord>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCreditId(
        [FromRoute] Guid creditId,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Fetching audit records for CreditId={CreditId}, Limit={Limit}",
            creditId, limit);

        var records = await auditRepository.GetByEntityIdAsync(creditId, limit, ct);

        logger.LogInformation(
            "Returning {Count} audit records for CreditId={CreditId}",
            records.Count(), creditId);

        return Ok(records);
    }
}
