namespace PortYard.Domain.Enums;

/// <summary>Category of a physical move recorded on a container's append-only movement ledger.</summary>
public enum MovementType
{
    GateIn = 0,
    Yard = 1,
    Stage = 2,
    GateOut = 3
}
