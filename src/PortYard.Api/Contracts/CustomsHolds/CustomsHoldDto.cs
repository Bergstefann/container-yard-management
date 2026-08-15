namespace PortYard.Api.Contracts.CustomsHolds;

public class CustomsHoldDto
{
    public required int Id { get; init; }
    public required string ContainerNumber { get; init; }
    public required string Reason { get; init; }
    public required DateTimeOffset PlacedAt { get; init; }
    public DateTimeOffset? ReleasedAt { get; init; }
    public required bool IsActive { get; init; }
}
