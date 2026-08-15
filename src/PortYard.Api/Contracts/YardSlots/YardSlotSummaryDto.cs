namespace PortYard.Api.Contracts.YardSlots;

public class YardSlotSummaryDto
{
    public required string Code { get; init; }
    public required string Block { get; init; }
    public required int Row { get; init; }
    public required int Tier { get; init; }
    public required int MaxTeu { get; init; }
    public required int UsedTeu { get; init; }
    public required int RemainingTeu { get; init; }
    public required bool IsReeferCapable { get; init; }
}
