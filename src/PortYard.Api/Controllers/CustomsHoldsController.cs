using Microsoft.AspNetCore.Mvc;
using PortYard.Api.Contracts.CustomsHolds;
using PortYard.Api.Services;

namespace PortYard.Api.Controllers;

/// <summary>Placing and releasing customs holds against containers.</summary>
[ApiController]
[Produces("application/json")]
public class CustomsHoldsController(CustomsHoldService customsHoldService) : ControllerBase
{
    /// <summary>Places a customs hold on a container, blocking gate-out until it's released.</summary>
    [HttpPost("/api/containers/{containerNumber}/holds")]
    [ProducesResponseType(typeof(CustomsHoldDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomsHoldDto>> PlaceHold(string containerNumber, PlaceHoldRequest request, CancellationToken ct)
    {
        var result = await customsHoldService.PlaceHoldAsync(containerNumber, request.Reason, ct);
        return Created($"/api/holds/{result.Id}", result);
    }

    /// <summary>Releases an active customs hold, permitting the container to gate out.</summary>
    [HttpPost("/api/holds/{id:int}/release")]
    [ProducesResponseType(typeof(CustomsHoldDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomsHoldDto>> Release(int id, CancellationToken ct)
    {
        var result = await customsHoldService.ReleaseHoldAsync(id, ct);
        return Ok(result);
    }
}
