using Asp.Versioning;
using Kriteriom.Credits.API.Metrics;
using Kriteriom.Credits.Application.Commands.CreateCredit;
using Kriteriom.Credits.Application.Commands.ProcessRiskAssessment;
using Kriteriom.Credits.Application.Commands.RecalculateCreditStatuses;
using Kriteriom.Credits.Application.Commands.UpdateCreditStatus;
using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Queries.GetCredit;
using Kriteriom.Credits.Application.Queries.GetCreditStats;
using Kriteriom.Credits.Application.Queries.GetCredits;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.SharedKernel.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kriteriom.Credits.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class CreditsController(IMediator mediator, ILogger<CreditsController> logger) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly ILogger<CreditsController> _logger = logger;

    /// <summary>
    /// Returns aggregate statistics: totals by status, approval rate, client count.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCreditStatsQuery(), ct);

        if (!result.IsSuccess)
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Query Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = result.Error
            });

        var s = result.Value!;
        return Ok(new
        {
            s.TotalCredits,
            s.TotalClients,
            s.ApprovalRate,
            byStatus = new
            {
                s.Pending, s.UnderReview, s.Approved,
                s.Rejected, s.Closed, s.Defaulted
            }
        });
    }

    /// <summary>
    /// Creates a new credit request.
    /// </summary>
    /// <param name="request">Credit creation parameters</param>
    /// <param name="idempotencyKey">Unique key to ensure idempotent processing</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created credit resource</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreditDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCredit(
        [FromBody] CreateCreditRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Header",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The Idempotency-Key header is required"
            });
        }

        var command = new CreateCreditCommand
        {
            ClientId = request.ClientId,
            Amount = request.Amount,
            InterestRate = request.InterestRate,
            TermMonths = request.TermMonths,
            IdempotencyKey = idempotencyKey
        };

        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Credit Creation Failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = result.Error,
                Extensions = { ["errorCode"] = result.ErrorCode }
            });
        }

        CreditMetrics.CreatedTotal.Inc();
        return CreatedAtAction(
            nameof(GetCredit),
            new { id = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Returns a paginated list of credits.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (max 100)</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="clientId">Client Id</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paged credit list</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CreditDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCredits(
        [FromQuery] int           page       = 1,
        [FromQuery] int           pageSize   = 20,
        [FromQuery] CreditStatus? status     = null,
        [FromQuery] Guid?         clientId   = null,
        [FromQuery] decimal?      amountMin  = null,
        [FromQuery] decimal?      amountMax  = null,
        [FromQuery] DateTime?     dateFrom   = null,
        [FromQuery] DateTime?     dateTo     = null,
        [FromQuery] string?       riskLevel  = null,
        [FromQuery] string?       clientName = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = new GetCreditsQuery(
            page, pageSize, status, clientId,
            amountMin, amountMax, dateFrom, dateTo,
            riskLevel, clientName);
        var result = await _mediator.Send(query, ct);

        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Query Failed",
                Status = StatusCodes.Status500InternalServerError,
                Detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Returns a specific credit by ID.
    /// </summary>
    /// <param name="id">Credit identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Credit resource or 404</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CreditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCredit([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetCreditQuery(id);
        var result = await _mediator.Send(query, ct);

        if (!result.IsSuccess)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Updates the status of an existing credit.
    /// </summary>
    /// <param name="id">Credit identifier</param>
    /// <param name="request">New status and optional reason</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated credit resource</returns>
    [AllowAnonymous]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(CreditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCreditStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateCreditStatusRequest request,
        CancellationToken ct)
    {
        var command = new UpdateCreditStatusCommand
        {
            CreditId = id,
            NewStatus = request.NewStatus,
            Reason = request.Reason
        };

        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "CREDIT_NOT_FOUND")
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = result.Error
                });
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Update Failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error,
                Extensions = { ["errorCode"] = result.ErrorCode }
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Processes a risk assessment result for a credit (internal endpoint used by risk service saga).
    /// </summary>
    /// <param name="id">Credit identifier</param>
    /// <param name="request">Risk assessment data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated credit with risk assessment applied</returns>
    [AllowAnonymous]
    [HttpPost("{id:guid}/risk")]
    [ProducesResponseType(typeof(CreditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessRiskAssessment(
        [FromRoute] Guid id,
        [FromBody] ProcessRiskAssessmentRequest request,
        CancellationToken ct)
    {
        var command = new ProcessRiskAssessmentCommand
        {
            CreditId = id,
            RiskScore = request.RiskScore,
            Decision = request.Decision,
            Reason = request.Reason
        };

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            switch (request.Decision)
            {
                case "Approved":    CreditMetrics.ApprovedTotal.Inc();    break;
                case "Rejected":    CreditMetrics.RejectedTotal.Inc();    break;
                case "UnderReview": CreditMetrics.UnderReviewTotal.Inc(); break;
            }
            return Ok(result.Value);
        }
        if (result.ErrorCode == "CREDIT_NOT_FOUND")
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error
            });
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Risk Assessment Failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = result.Error,
            Extensions = { ["errorCode"] = result.ErrorCode }
        });

    }

    [AllowAnonymous]
    [HttpPost("recalculate-statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecalculateStatuses(
        [FromQuery] int batchSize = 500, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new RecalculateCreditStatusesCommand(batchSize), ct);

        if (!result.IsSuccess)
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Recalculation Failed", Detail = result.Error
            });

        return Ok(result.Value);
    }
}

// Request models — separate from commands to decouple HTTP concerns from application layer

public record CreateCreditRequest(Guid ClientId, decimal Amount, decimal InterestRate, int TermMonths = 36);

public record UpdateCreditStatusRequest(CreditStatus NewStatus, string? Reason);

public record ProcessRiskAssessmentRequest(decimal RiskScore, string Decision, string Reason);
