namespace PortYard.Domain.Enums;

/// <summary>
/// Lifecycle state of a container on the terminal. Legal transitions are enforced
/// by <see cref="PortYard.Domain.Entities.Container"/>, not by callers.
/// </summary>
public enum ContainerStatus
{
    /// <summary>Pre-advised by the shipping line, not yet physically on terminal.</summary>
    Expected = 0,

    /// <summary>Through the gate, no yard slot assigned yet.</summary>
    GatedIn = 1,

    /// <summary>Occupying a yard slot.</summary>
    Stored = 2,

    /// <summary>Lifted from its slot and staged for departure.</summary>
    Staged = 3,

    /// <summary>Left the terminal. Terminal state — nothing transitions out of it.</summary>
    GatedOut = 4
}
