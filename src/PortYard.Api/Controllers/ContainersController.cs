using Microsoft.AspNetCore.Mvc;
using PortYard.Api.Contracts.Common;
using PortYard.Api.Contracts.Containers;
using PortYard.Api.Services;
using PortYard.Domain.Enums;

namespace PortYard.Api.Controllers;

/// <summary>Registration, gate movements, and lookups for containers on the terminal.</summary>
[ApiController]
[Route("api/containers")]
[Produces("application/json")]
public class ContainersController(ContainerService containerService) : ControllerBase
{
    /// <summary>Lists containers, optionally filtered, with pagination metadata.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContainerSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ContainerSummaryDto>>> GetAll(
        [FromQuery] ContainerStatus? status,
        [FromQuery] ContainerSize? size,
        [FromQuery] ContainerType? type,
        [FromQuery] string? shippingLine,
        [FromQuery] string? block,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await containerService.GetPagedAsync(status, size, type, shippingLine, block, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Full detail for a single container: current slot, active holds, and movement history.</summary>
    [HttpGet("{containerNumber}")]
    [ProducesResponseType(typeof(ContainerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContainerDetailDto>> GetByNumber(string containerNumber, CancellationToken ct)
    {
        var result = await containerService.GetDetailAsync(containerNumber, ct);
        return Ok(result);
    }

    /// <summary>Registers a container that has been pre-advised by its shipping line but has not yet arrived.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContainerSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContainerSummaryDto>> Register(RegisterContainerRequest request, CancellationToken ct)
    {
        var result = await containerService.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetByNumber), new { containerNumber = result.ContainerNumber }, result);
    }

    /// <summary>Gates a container in: Expected -> GatedIn.</summary>
    [HttpPost("{containerNumber}/gate-in")]
    [ProducesResponseType(typeof(ContainerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContainerSummaryDto>> GateIn(string containerNumber, CancellationToken ct)
    {
        var result = await containerService.GateInAsync(containerNumber, ct);
        return Ok(result);
    }

    /// <summary>Assigns the container to a yard slot: GatedIn/Staged -> Stored, or a yard shuffle if already Stored.</summary>
    [HttpPost("{containerNumber}/assign-slot")]
    [ProducesResponseType(typeof(ContainerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContainerSummaryDto>> AssignSlot(string containerNumber, AssignSlotRequest request, CancellationToken ct)
    {
        var result = await containerService.AssignSlotAsync(containerNumber, request.SlotCode, ct);
        return Ok(result);
    }

    /// <summary>Stages the container for departure: GatedIn/Stored -> Staged.</summary>
    [HttpPost("{containerNumber}/stage")]
    [ProducesResponseType(typeof(ContainerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContainerSummaryDto>> Stage(string containerNumber, CancellationToken ct)
    {
        var result = await containerService.StageAsync(containerNumber, ct);
        return Ok(result);
    }

    /// <summary>Gates the container out: Staged -> GatedOut. Blocked while any customs hold is active.</summary>
    [HttpPost("{containerNumber}/gate-out")]
    [ProducesResponseType(typeof(ContainerSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContainerSummaryDto>> GateOut(string containerNumber, CancellationToken ct)
    {
        var result = await containerService.GateOutAsync(containerNumber, ct);
        return Ok(result);
    }
}
