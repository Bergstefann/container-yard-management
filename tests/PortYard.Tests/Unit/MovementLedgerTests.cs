using FluentAssertions;
using PortYard.Domain.Entities;
using PortYard.Domain.Enums;
using PortYard.Domain.Exceptions;

namespace PortYard.Tests.Unit;

public class MovementLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Container NewContainer() =>
        Container.Register("MSCU1234566", ContainerSize.TwentyFoot, ContainerType.DryVan, 10_000, "MSC");

    [Fact]
    public void Movements_are_recorded_in_chronological_order()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 4, isReeferCapable: false);
        var container = NewContainer();

        container.GateIn(T0, "operator");
        container.AssignToSlot(slot, T0.AddHours(1), "operator");
        container.Stage(T0.AddHours(2), "operator");

        container.Movements.Select(m => m.Type).Should().Equal(
            MovementType.GateIn, MovementType.Yard, MovementType.Stage);
        container.Movements.Select(m => m.OccurredAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public void A_movement_dated_before_the_previous_one_throws()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 4, isReeferCapable: false);
        var container = NewContainer();
        container.GateIn(T0, "operator");

        var act = () => container.AssignToSlot(slot, T0.AddHours(-1), "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void Movement_history_is_never_mutated_by_later_operations()
    {
        var slotA = YardSlot.Create("A", 1, 1, maxTeu: 4, isReeferCapable: false);
        var slotB = YardSlot.Create("B", 1, 1, maxTeu: 4, isReeferCapable: false);
        var container = NewContainer();

        container.GateIn(T0, "operator");
        var gateInMovement = container.Movements.Single();
        var gateInSnapshot = (gateInMovement.Type, gateInMovement.FromSlotId, gateInMovement.ToSlotId, gateInMovement.OccurredAt, gateInMovement.Operator);

        container.AssignToSlot(slotA, T0.AddHours(1), "operator");
        container.AssignToSlot(slotB, T0.AddHours(2), "operator");
        container.Stage(T0.AddHours(3), "operator");

        (gateInMovement.Type, gateInMovement.FromSlotId, gateInMovement.ToSlotId, gateInMovement.OccurredAt, gateInMovement.Operator)
            .Should().Be(gateInSnapshot);
        container.Movements.Should().HaveCount(4);
    }
}
