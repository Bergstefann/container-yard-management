namespace PortYard.Api.Contracts.Reports;

public class ThroughputReportEntryDto
{
    public required DateOnly Date { get; init; }
    public required int GateInCount { get; init; }
    public required int GateOutCount { get; init; }
}
