using PortYard.Api.Contracts.CustomsHolds;
using PortYard.Domain.Enums;

namespace PortYard.Api.Contracts.Containers;

public class ContainerDetailDto
{
    public required string ContainerNumber { get; init; }
    public required ContainerSize Size { get; init; }
    public required ContainerType Type { get; init; }
    public required ContainerStatus Status { get; init; }
    public required string ShippingLine { get; init; }
    public required int GrossWeightKg { get; init; }
    public string? CurrentSlotCode { get; init; }
    public string? InboundVesselName { get; init; }
    public DateTimeOffset? ArrivedAt { get; init; }
    public DateTimeOffset? DepartedAt { get; init; }
    public required IReadOnlyList<CustomsHoldDto> ActiveHolds { get; init; }
    public required IReadOnlyList<MovementDto> Movements { get; init; }
}
