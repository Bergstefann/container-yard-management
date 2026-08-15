using PortYard.Domain.Enums;

namespace PortYard.Api.Contracts.Containers;

/// <summary>Row-level shape for container listing endpoints.</summary>
public class ContainerSummaryDto
{
    public required string ContainerNumber { get; init; }
    public required ContainerSize Size { get; init; }
    public required ContainerType Type { get; init; }
    public required ContainerStatus Status { get; init; }
    public required string ShippingLine { get; init; }
    public required int GrossWeightKg { get; init; }
    public string? CurrentSlotCode { get; init; }
    public DateTimeOffset? ArrivedAt { get; init; }
    public DateTimeOffset? DepartedAt { get; init; }
}
