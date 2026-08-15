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

    /// <summary>
    /// Derived from <see cref="FromSlot"/> rather than stored directly. Recording the slot id
    /// eagerly at call time is wrong during bulk seeding: entities are linked in memory before
    /// EF assigns real primary keys, so an eagerly-read id would still be 0. Reading it off the
    /// navigation instead stays correct whether or not the slot has been persisted yet.
    /// </summary>
    public int? FromSlotId => FromSlot?.Id;
    public YardSlot? FromSlot { get; private set; }

    public int? ToSlotId => ToSlot?.Id;
    public YardSlot? ToSlot { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
    public string Operator { get; private set; } = string.Empty;

    private Movement()
    {
    }

    /// <summary>Only <see cref="Container"/> creates movements — it owns chronology and transition validation.</summary>
    internal Movement(Container container, MovementType type, YardSlot? fromSlot, YardSlot? toSlot, DateTimeOffset occurredAt, string operatorName)
    {
        Container = container;
        Type = type;
        FromSlot = fromSlot;
        ToSlot = toSlot;
        OccurredAt = occurredAt;
        Operator = operatorName;
    }
}
