using PortYard.Domain.Enums;
using PortYard.Domain.Exceptions;
using PortYard.Domain.Validation;

namespace PortYard.Domain.Entities;

/// <summary>
/// A shipping container tracked through the terminal. All state changes go through this
/// class's methods so that status transitions, slot capacity, and movement chronology can
/// never be violated by a caller poking at properties directly.
/// </summary>
public class Container
{
    public int Id { get; private set; }

    /// <summary>ISO 6346 container number, normalised to upper case (e.g. "MSCU1234566").</summary>
    public string ContainerNumber { get; private set; } = string.Empty;

    public ContainerSize Size { get; private set; }
    public ContainerType Type { get; private set; }
    public ContainerStatus Status { get; private set; }
    public int GrossWeightKg { get; private set; }
    public string ShippingLine { get; private set; } = string.Empty;

    public int? CurrentSlotId { get; private set; }
    public YardSlot? CurrentSlot { get; private set; }

    public int? InboundVesselId { get; private set; }
    public Vessel? InboundVessel { get; private set; }

    public DateTimeOffset? ArrivedAt { get; private set; }
    public DateTimeOffset? DepartedAt { get; private set; }

    private readonly List<Movement> _movements = [];
    public IReadOnlyCollection<Movement> Movements => _movements.AsReadOnly();

    private readonly List<CustomsHold> _customsHolds = [];
    public IReadOnlyCollection<CustomsHold> CustomsHolds => _customsHolds.AsReadOnly();

    /// <summary>TEU (twenty-foot equivalent unit) footprint used for slot capacity checks.</summary>
    public int Teu => Size == ContainerSize.TwentyFoot ? 1 : 2;

    private Container()
    {
    }

    /// <summary>Registers a new container that has been pre-advised by its shipping line but has not yet arrived.</summary>
    public static Container Register(
        string containerNumber,
        ContainerSize size,
        ContainerType type,
        int grossWeightKg,
        string shippingLine,
        Vessel? inboundVessel = null)
    {
        if (!Iso6346.IsValid(containerNumber))
            throw new DomainRuleException($"'{containerNumber}' is not a valid ISO 6346 container number.");

        if (grossWeightKg <= 0)
            throw new DomainRuleException("Gross weight must be positive.");

        if (string.IsNullOrWhiteSpace(shippingLine))
            throw new DomainRuleException("Shipping line is required.");

        return new Container
        {
            ContainerNumber = containerNumber.Trim().ToUpperInvariant(),
            Size = size,
            Type = type,
            GrossWeightKg = grossWeightKg,
            ShippingLine = shippingLine.Trim(),
            Status = ContainerStatus.Expected,
            InboundVessel = inboundVessel,
            InboundVesselId = inboundVessel?.Id
        };
    }

    /// <summary>Expected → GatedIn. The container has physically arrived through the gate.</summary>
    public void GateIn(DateTimeOffset occurredAt, string operatorName)
    {
        if (Status != ContainerStatus.Expected)
            throw new DomainRuleException($"Cannot gate in a container with status {Status}; it must be {ContainerStatus.Expected}.");

        Status = ContainerStatus.GatedIn;
        ArrivedAt = occurredAt;
        RecordMovement(MovementType.GateIn, null, null, occurredAt, operatorName);
    }

    /// <summary>
    /// Assigns the container to a yard slot: GatedIn/Staged → Stored (first stow or re-yard),
    /// or Stored → Stored at a different slot (a yard shuffle). Clears any previous slot and
    /// records a Yard movement with both the from and to slot.
    /// </summary>
    public void AssignToSlot(YardSlot slot, DateTimeOffset occurredAt, string operatorName)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (Status is not (ContainerStatus.GatedIn or ContainerStatus.Staged or ContainerStatus.Stored))
            throw new DomainRuleException($"Cannot assign a slot to a container with status {Status}.");

        if (Type == ContainerType.Reefer && !slot.IsReeferCapable)
            throw new DomainRuleException($"Slot {slot.Code} is not reefer-capable; cannot store a reefer there.");

        var occupiedTeu = slot.Containers.Where(c => !ReferenceEquals(c, this)).Sum(c => c.Teu);
        if (occupiedTeu + Teu > slot.MaxTeu)
            throw new DomainRuleException($"Slot {slot.Code} has no capacity for another {Teu} TEU ({occupiedTeu}/{slot.MaxTeu} TEU used).");

        var fromSlotId = CurrentSlotId;

        CurrentSlot?.RemoveContainer(this);
        slot.AddContainer(this);

        CurrentSlot = slot;
        CurrentSlotId = slot.Id;
        Status = ContainerStatus.Stored;

        RecordMovement(MovementType.Yard, fromSlotId, slot.Id, occurredAt, operatorName);
    }

    /// <summary>GatedIn → Staged (direct transhipment, never yarded) or Stored → Staged (normal flow, lifted from its slot).</summary>
    public void Stage(DateTimeOffset occurredAt, string operatorName)
    {
        if (Status is not (ContainerStatus.GatedIn or ContainerStatus.Stored))
            throw new DomainRuleException($"Cannot stage a container with status {Status}.");

        var fromSlotId = CurrentSlotId;

        CurrentSlot?.RemoveContainer(this);
        CurrentSlot = null;
        CurrentSlotId = null;
        Status = ContainerStatus.Staged;

        RecordMovement(MovementType.Stage, fromSlotId, null, occurredAt, operatorName);
    }

    /// <summary>Staged → GatedOut, the terminal state. Blocked while any customs hold is active.</summary>
    public void GateOut(DateTimeOffset occurredAt, string operatorName)
    {
        if (Status != ContainerStatus.Staged)
            throw new DomainRuleException($"Cannot gate out a container with status {Status}; it must be {ContainerStatus.Staged}.");

        if (_customsHolds.Any(h => h.IsActive))
            throw new DomainRuleException("Cannot gate out a container with an active customs hold.");

        Status = ContainerStatus.GatedOut;
        DepartedAt = occurredAt;
        RecordMovement(MovementType.GateOut, null, null, occurredAt, operatorName);
    }

    public CustomsHold PlaceHold(string reason, DateTimeOffset placedAt)
    {
        if (Status == ContainerStatus.GatedOut)
            throw new DomainRuleException("Cannot place a customs hold on a container that has already gated out.");

        var hold = new CustomsHold(this, reason, placedAt);
        _customsHolds.Add(hold);
        return hold;
    }

    private void RecordMovement(MovementType type, int? fromSlotId, int? toSlotId, DateTimeOffset occurredAt, string operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
            throw new DomainRuleException("An operator is required to record a movement.");

        var lastMovementAt = _movements.Count == 0 ? (DateTimeOffset?)null : _movements.Max(m => m.OccurredAt);
        if (lastMovementAt is not null && occurredAt < lastMovementAt)
            throw new DomainRuleException("A movement cannot be dated earlier than the container's most recent movement.");

        _movements.Add(new Movement(this, type, fromSlotId, toSlotId, occurredAt, operatorName.Trim()));
    }
}
