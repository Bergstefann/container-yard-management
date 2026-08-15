using PortYard.Domain.Enums;

namespace PortYard.Api.Contracts.Containers;

public class MovementDto
{
    public required MovementType Type { get; init; }
    public string? FromSlotCode { get; init; }
    public string? ToSlotCode { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string Operator { get; init; }
}
