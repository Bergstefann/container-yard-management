using System.ComponentModel.DataAnnotations;
using PortYard.Api.Contracts.Validation;
using PortYard.Domain.Enums;

namespace PortYard.Api.Contracts.Containers;

/// <summary>Registers a container that has been pre-advised by its shipping line but has not yet arrived.</summary>
public class RegisterContainerRequest
{
    /// <summary>ISO 6346 container number, e.g. "MSCU1234566".</summary>
    [Required]
    [Iso6346]
    public required string ContainerNumber { get; init; }

    public required ContainerSize Size { get; init; }
    public required ContainerType Type { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Gross weight must be positive.")]
    public required int GrossWeightKg { get; init; }

    [Required]
    public required string ShippingLine { get; init; }

    public int? InboundVesselId { get; init; }
}
