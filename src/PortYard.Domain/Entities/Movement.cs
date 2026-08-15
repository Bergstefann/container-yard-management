using PortYard.Domain.Enums;

namespace PortYard.Domain.Entities;

/// <summary>
/// An append-only record of a single physical move of a container. Never mutated or deleted
/// after creation — the ledger is the audit trail of everything that happened to a box.
/// </summary>
public class Movement
{
    public int Id { get; private set; }
    public int ContainerId { get; private set; }
    public Container Container { get; private set; } = null!;
    public MovementType Type { get; private set; }
    public int? FromSlotId { get; private set; }
    public int? ToSlotId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Operator { get; private set; } = string.Empty;

    private Movement()
    {
    }

    /// <summary>Only <see cref="Container"/> creates movements — it owns chronology and transition validation.</summary>
    internal Movement(Container container, MovementType type, int? fromSlotId, int? toSlotId, DateTimeOffset occurredAt, string operatorName)
    {
        Container = container;
        Type = type;
        FromSlotId = fromSlotId;
        ToSlotId = toSlotId;
        OccurredAt = occurredAt;
        Operator = operatorName;
    }
}
