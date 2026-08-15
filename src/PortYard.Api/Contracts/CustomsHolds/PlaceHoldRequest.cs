using System.ComponentModel.DataAnnotations;

namespace PortYard.Api.Contracts.CustomsHolds;

public class PlaceHoldRequest
{
    [Required]
    public required string Reason { get; init; }
}
