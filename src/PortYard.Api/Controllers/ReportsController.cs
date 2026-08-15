using Microsoft.AspNetCore.Mvc;
using PortYard.Api.Contracts.Reports;
using PortYard.Api.Services;

namespace PortYard.Api.Controllers;

/// <summary>Yard utilisation, dwell-time, and throughput reporting.</summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ReportsController(ReportService reportService) : ControllerBase
{
    /// <summary>Occupancy by block: slots used, TEU used vs capacity, and utilisation percentage.</summary>
    [HttpGet("yard-utilisation")]
    [ProducesResponseType(typeof(IReadOnlyList<YardUtilisationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<YardUtilisationDto>>> GetYardUtilisation(CancellationToken ct)
    {
        var result = await reportService.GetYardUtilisationAsync(ct);
        return Ok(result);
    }

    /// <summary>Average and median dwell time (gate-in to gate-out), grouped by shipping line and container type, for departed containers.</summary>
    [HttpGet("dwell-time")]
    [ProducesResponseType(typeof(IReadOnlyList<DwellTimeReportEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DwellTimeReportEntryDto>>> GetDwellTime(CancellationToken ct)
    {
        var result = await reportService.GetDwellTimeReportAsync(ct);
        return Ok(result);
    }

    /// <summary>Gate-in and gate-out counts bucketed by day within a date range. Defaults to the last 30 days.</summary>
    [HttpGet("throughput")]
    [ProducesResponseType(typeof(IReadOnlyList<ThroughputReportEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ThroughputReportEntryDto>>> GetThroughput(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var effectiveTo = to ?? DateTimeOffset.UtcNow;
        var effectiveFrom = from ?? effectiveTo.AddDays(-30);

        if (effectiveFrom > effectiveTo)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid date range",
                Detail = "'from' must not be after 'to'.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await reportService.GetThroughputReportAsync(effectiveFrom, effectiveTo, ct);
        return Ok(result);
    }
}
