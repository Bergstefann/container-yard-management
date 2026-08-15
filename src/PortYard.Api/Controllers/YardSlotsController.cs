using Microsoft.AspNetCore.Mvc;
using PortYard.Api.Contracts.YardSlots;
using PortYard.Api.Services;

namespace PortYard.Api.Controllers;

/// <summary>Yard slot occupancy and capacity lookups.</summary>
[ApiController]
[Route("api/slots")]
[Produces("application/json")]
public class YardSlotsController(YardSlotService yardSlotService) : ControllerBase
{
    /// <summary>Lists every yard slot with current occupancy and remaining TEU capacity.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<YardSlotSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YardSlotSummaryDto>>> GetAll(CancellationToken ct)
    {
        var result = await yardSlotService.GetAllAsync(ct);
        return Ok(result);
    }

    /// <summary>Detail for a single slot, including the containers currently occupying it.</summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(YardSlotDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<YardSlotDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        var result = await yardSlotService.GetDetailAsync(code, ct);
        return Ok(result);
    }
}
