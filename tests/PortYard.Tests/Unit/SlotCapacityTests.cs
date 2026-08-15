using FluentAssertions;
using PortYard.Domain.Entities;
using PortYard.Domain.Enums;
using PortYard.Domain.Exceptions;

namespace PortYard.Tests.Unit;

public class SlotCapacityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Container GatedInContainer(string containerNumber, ContainerSize size, ContainerType type = ContainerType.DryVan, DateTimeOffset? gateInAt = null)
    {
        var container = Container.Register(containerNumber, size, type, 10_000, "MSC");
        container.GateIn(gateInAt ?? T0, "operator");
        return container;
    }

    [Fact]
    public void Filling_a_slot_exactly_to_MaxTeu_succeeds()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 2, isReeferCapable: false);
        var container = GatedInContainer("MSCU1234566", ContainerSize.FortyFoot);

        container.AssignToSlot(slot, T0.AddHours(1), "operator");

        slot.UsedTeu.Should().Be(2);
        slot.RemainingTeu.Should().Be(0);
    }

    [Fact]
    public void Assigning_one_TEU_over_capacity_throws()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 2, isReeferCapable: false);
        var first = GatedInContainer("MSCU1234566", ContainerSize.FortyFoot);
        first.AssignToSlot(slot, T0.AddHours(1), "operator");

        var second = GatedInContainer("CSQU3054383", ContainerSize.TwentyFoot, gateInAt: T0.AddHours(2));

        var act = () => second.AssignToSlot(slot, T0.AddHours(3), "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void A_forty_foot_container_counts_as_two_TEU()
    {
        var container = Container.Register("MSCU1234566", ContainerSize.FortyFoot, ContainerType.DryVan, 10_000, "MSC");

        container.Teu.Should().Be(2);
    }

    [Fact]
    public void A_twenty_foot_container_counts_as_one_TEU()
    {
        var container = Container.Register("CSQU3054383", ContainerSize.TwentyFoot, ContainerType.DryVan, 10_000, "MSC");

        container.Teu.Should().Be(1);
    }

    [Fact]
    public void Reefer_into_non_reefer_capable_slot_throws()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 4, isReeferCapable: false);
        var container = GatedInContainer("MSCU1234566", ContainerSize.FortyFoot, ContainerType.Reefer);

        var act = () => container.AssignToSlot(slot, T0.AddHours(1), "operator");

        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void Reefer_into_reefer_capable_slot_succeeds()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 4, isReeferCapable: true);
        var container = GatedInContainer("MSCU1234566", ContainerSize.FortyFoot, ContainerType.Reefer);

        container.AssignToSlot(slot, T0.AddHours(1), "operator");

        container.Status.Should().Be(ContainerStatus.Stored);
        slot.Containers.Should().Contain(container);
    }

    [Fact]
    public void Removing_a_container_frees_capacity_for_a_subsequent_assignment()
    {
        var slot = YardSlot.Create("A", 1, 1, maxTeu: 2, isReeferCapable: false);
        var first = GatedInContainer("MSCU1234566", ContainerSize.FortyFoot);
        first.AssignToSlot(slot, T0.AddHours(1), "operator");

        // Stage lifts the first container out of the slot, freeing its 2 TEU.
        first.Stage(T0.AddHours(2), "operator");

        var second = GatedInContainer("CSQU3054383", ContainerSize.FortyFoot, gateInAt: T0.AddHours(3));
        var act = () => second.AssignToSlot(slot, T0.AddHours(4), "operator");

        act.Should().NotThrow();
        slot.Containers.Should().ContainSingle().Which.Should().Be(second);
    }
}
