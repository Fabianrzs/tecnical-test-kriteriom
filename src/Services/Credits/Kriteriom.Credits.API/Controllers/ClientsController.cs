using Asp.Versioning;
using Kriteriom.Credits.Application.Commands.CreateClient;
using Kriteriom.Credits.Application.Commands.UpdateClient;
using Kriteriom.Credits.Application.DTOs;
using Kriteriom.Credits.Application.Queries.GetClient;
using Kriteriom.Credits.Application.Queries.GetClientFinancialSummary;
using Kriteriom.Credits.Application.Queries.GetClients;
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
public class ClientsController(IMediator mediator) : ControllerBase
{
    /// <summary>Creates a new client profile.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var command = new CreateClientCommand
        {
            FullName         = request.FullName,
            Email            = request.Email,
            DocumentNumber   = request.DocumentNumber,
            MonthlyIncome    = request.MonthlyIncome,
            EmploymentStatus = request.EmploymentStatus
        };

        var result = await mediator.Send(command, ct);
        if (!result.IsSuccess)
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Client Creation Failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = result.Error,
                Extensions = { ["errorCode"] = result.ErrorCode }
            });

        return CreatedAtAction(nameof(GetClient), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Returns a paginated list of clients.
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (max 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var result = await mediator.Send(new GetClientsQuery(page, pageSize), ct);
        return Ok(result.Value);
    }

    /// <summary>
    /// Returns a client by ID.
    /// <param name="id">Client identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClient([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetClientQuery(id), ct);
        if (!result.IsSuccess)
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>Returns all credits for a specific client.</summary>
    [HttpGet("{id:guid}/credits")]
    [ProducesResponseType(typeof(PagedResult<CreditDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientCredits(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var result = await mediator.Send(new GetCreditsQuery(page, pageSize, null, id), ct);
        if (!result.IsSuccess)
            return StatusCode(500, new ProblemDetails { Title = "Query Failed", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>Returns a client's existing monthly debt load from active credits.</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/financial-summary")]
    [ProducesResponseType(typeof(ClientFinancialSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFinancialSummary([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetClientFinancialSummaryQuery(id), ct);
        if (!result.IsSuccess)
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = result.Error
            });

        return Ok(result.Value);
    }

    /// <summary>Updates a client's mutable fields (name, income, score, employment).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateClient(
        [FromRoute] Guid id,
        [FromBody] UpdateClientRequest request,
        CancellationToken ct)
    {
        var command = new UpdateClientCommand
        {
            ClientId         = id,
            FullName         = request.FullName,
            MonthlyIncome    = request.MonthlyIncome,
            EmploymentStatus = request.EmploymentStatus
        };

        var result = await mediator.Send(command, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "CLIENT_NOT_FOUND")
                return NotFound(new ProblemDetails { Title = "Not Found", Status = 404, Detail = result.Error });

            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Update Failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = result.Error
            });
        }

        return Ok(result.Value);
    }
}

public record CreateClientRequest(
    string FullName,
    string Email,
    string DocumentNumber,
    decimal MonthlyIncome,
    EmploymentStatus EmploymentStatus);

public record UpdateClientRequest(
    string FullName,
    decimal MonthlyIncome,
    EmploymentStatus EmploymentStatus);
