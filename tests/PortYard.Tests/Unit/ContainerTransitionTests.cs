using FluentAssertions;
using PortYard.Domain.Entities;
using PortYard.Domain.Enums;
using PortYard.Domain.Exceptions;

namespace PortYard.Tests.Unit;

public class ContainerTransitionTests
{
    private static readonly DateTimeOffset FarFuture = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public enum Fixture { Expected, GatedIn, Stored, Staged, GatedOut }

    private static Container NewContainer() =>
        Container.Register("MSCU1234566", ContainerSize.TwentyFoot, ContainerType.DryVan, 10_000, "MSC");

    private static YardSlot NewSlot(string block = "A", bool reeferCapable = false) =>
        YardSlot.Create(block, 1, 1, maxTeu: 4, reeferCapable);

    private static (Container Container, YardSlot Slot) BuildFixture(Fixture fixture)
    {
        var slot = NewSlot();
        var container = NewContainer();
        var t = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        if (fixture == Fixture.Expected)
            return (container, slot);

        container.GateIn(t, "operator");
        if (fixture == Fixture.GatedIn)
            return (container, slot);

        container.AssignToSlot(slot, t.AddHours(1), "operator");
        if (fixture == Fixture.Stored)
            return (container, slot);

        container.Stage(t.AddHours(2), "operator");
        if (fixture == Fixture.Staged)
            return (container, slot);

        container.GateOut(t.AddHours(3), "operator");
        return (container, slot);
    }

    [Fact]
    public void Register_creates_container_in_Expected_status()
    {
        var container = NewContainer();

        container.Status.Should().Be(ContainerStatus.Expected);
    }

    [Fact]
    public void GateIn_transitions_Expected_to_GatedIn_and_sets_ArrivedAt()
    {
        var container = NewContainer();
        var t = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        container.GateIn(t, "operator");

        container.Status.Should().Be(ContainerStatus.GatedIn);
        container.ArrivedAt.Should().Be(t);
    }

    [Fact]
    public void AssignToSlot_transitions_GatedIn_to_Stored()
    {
        var (container, slot) = BuildFixture(Fixture.GatedIn);

        container.AssignToSlot(slot, FarFuture, "operator");

        container.Status.Should().Be(ContainerStatus.Stored);
        container.CurrentSlot.Should().Be(slot);
    }

    [Fact]
    public void Stage_transitions_Stored_to_Staged_and_clears_the_slot()
    {
        var (container, slot) = BuildFixture(Fixture.Stored);

        container.Stage(FarFuture, "operator");

        container.Status.Should().Be(ContainerStatus.Staged);
        container.CurrentSlot.Should().BeNull();
        slot.Containers.Should().NotContain(container);
    }

    [Fact]
    public void Stage_transitions_GatedIn_directly_to_Staged_for_direct_transhipment()
    {
        var (container, _) = BuildFixture(Fixture.GatedIn);

        container.Stage(FarFuture, "operator");

        container.Status.Should().Be(ContainerStatus.Staged);
    }

    [Fact]
    public void AssignToSlot_transitions_Staged_to_Stored_when_reyarded()
    {
        var (container, slot) = BuildFixture(Fixture.Staged);

        container.AssignToSlot(slot, FarFuture, "operator");

        container.Status.Should().Be(ContainerStatus.Stored);
        container.CurrentSlot.Should().Be(slot);
    }

    [Fact]
    public void GateOut_transitions_Staged_to_GatedOut_and_sets_DepartedAt()
    {
        var (container, _) = BuildFixture(Fixture.Staged);

        container.GateOut(FarFuture, "operator");

        container.Status.Should().Be(ContainerStatus.GatedOut);
        container.DepartedAt.Should().Be(FarFuture);
    }

    [Theory]
    [InlineData(Fixture.GatedIn)]
    [InlineData(Fixture.Stored)]
    [InlineData(Fixture.Staged)]
    [InlineData(Fixture.GatedOut)]
    public void GateIn_throws_unless_status_is_Expected(Fixture fixture)
    {
        var (container, _) = BuildFixture(fixture);

        var act = () => container.GateIn(FarFuture, "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Theory]
    [InlineData(Fixture.Expected)]
    [InlineData(Fixture.GatedOut)]
    public void AssignToSlot_throws_when_status_is_Expected_or_GatedOut(Fixture fixture)
    {
        var (container, _) = BuildFixture(fixture);
        var slot = NewSlot("B");

        var act = () => container.AssignToSlot(slot, FarFuture, "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Theory]
    [InlineData(Fixture.Expected)]
    [InlineData(Fixture.GatedOut)]
    public void Stage_throws_when_status_is_Expected_or_GatedOut(Fixture fixture)
    {
        var (container, _) = BuildFixture(fixture);

        var act = () => container.Stage(FarFuture, "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Theory]
    [InlineData(Fixture.Expected)]
    [InlineData(Fixture.GatedIn)]
    [InlineData(Fixture.Stored)]
    [InlineData(Fixture.GatedOut)]
    public void GateOut_throws_unless_status_is_Staged(Fixture fixture)
    {
        var (container, _) = BuildFixture(fixture);

        var act = () => container.GateOut(FarFuture, "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void GateOut_throws_when_an_active_customs_hold_exists()
    {
        var (container, _) = BuildFixture(Fixture.Staged);
        container.PlaceHold("Random inspection", FarFuture);

        var act = () => container.GateOut(FarFuture.AddHours(1), "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void GateOut_succeeds_after_the_hold_is_released()
    {
        var (container, _) = BuildFixture(Fixture.Staged);
        var hold = container.PlaceHold("Random inspection", FarFuture);
        hold.Release(FarFuture.AddHours(1));

        container.GateOut(FarFuture.AddHours(2), "operator");

        container.Status.Should().Be(ContainerStatus.GatedOut);
    }

    [Fact]
    public void AssignToSlot_clears_previous_slot_and_records_movement_with_correct_from_and_to()
    {
        var slotA = NewSlot("A");
        var slotB = NewSlot("B");
        var container = NewContainer();
        var t = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        container.GateIn(t, "operator");
        container.AssignToSlot(slotA, t.AddHours(1), "operator");

        container.AssignToSlot(slotB, t.AddHours(2), "operator");

        container.CurrentSlot.Should().Be(slotB);
        slotA.Containers.Should().NotContain(container);
        slotB.Containers.Should().Contain(container);

        var lastMovement = container.Movements.Last();
        lastMovement.Type.Should().Be(MovementType.Yard);
        lastMovement.FromSlot.Should().Be(slotA);
        lastMovement.ToSlot.Should().Be(slotB);
    }
}
