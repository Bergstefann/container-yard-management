using PortYard.Api.Contracts.Containers;

namespace PortYard.Api.Contracts.YardSlots;

public class YardSlotDetailDto : YardSlotSummaryDto
{
    public required IReadOnlyList<ContainerSummaryDto> Containers { get; init; }
}
