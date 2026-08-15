using System.ComponentModel.DataAnnotations;

namespace PortYard.Api.Contracts.Containers;

public class AssignSlotRequest
{
    /// <summary>Slot code in "Block+Row-Tier" form, e.g. "A05-1".</summary>
    [Required]
    public required string SlotCode { get; init; }
}
