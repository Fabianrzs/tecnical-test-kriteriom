using Kriteriom.Notifications.Domain.Entities;
using Kriteriom.Notifications.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Kriteriom.Notifications.API.Controllers;

[ApiController]
[Route("notifications")]
public class NotificationsController(
    INotificationRepository repository,
    ILogger<NotificationsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        NotificationStatus? parsedStatus = null;
        if (status is not null && Enum.TryParse<NotificationStatus>(status, ignoreCase: true, out var s))
            parsedStatus = s;

        var (items, total) = await repository.GetPagedAsync(page, pageSize, parsedStatus, ct);

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            items = items.Select(MapToResponse)
        });
    }

    [HttpGet("credit/{creditId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCreditId(Guid creditId, CancellationToken ct = default)
    {
        var items = await repository.GetByCreditIdAsync(creditId, ct);
        logger.LogInformation("Queried {Count} notifications for credit {CreditId}", items.Count, creditId);
        return Ok(items.Select(MapToResponse));
    }

    private static object MapToResponse(NotificationRecord n) => new
    {
        n.Id,
        n.CreditId,
        n.EventType,
        n.Recipient,
        n.Subject,
        Status     = n.Status.ToString(),
        n.RetryCount,
        n.ErrorMessage,
        n.CreatedAt,
        n.SentAt
    };
}
