using PortYard.Domain.Enums;

namespace PortYard.Api.Contracts.Reports;

public class DwellTimeReportEntryDto
{
    public required string ShippingLine { get; init; }
    public required ContainerType ContainerType { get; init; }
    public required double AverageDwellHours { get; init; }
    public required double MedianDwellHours { get; init; }
    public required int SampleSize { get; init; }
}
