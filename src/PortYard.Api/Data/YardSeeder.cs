using PortYard.Domain.Entities;
using PortYard.Domain.Enums;
using PortYard.Domain.Validation;

namespace PortYard.Api.Data;

/// <summary>
/// Builds a realistic Antwerp-flavoured demo dataset: four vessels, forty yard slots across
/// blocks A-D, and sixty containers driven through their real lifecycle methods so that status,
/// movement history, and holds stay internally consistent. Idempotent — a no-op if the database
/// already has data. Uses a fixed anchor date rather than <see cref="DateTimeOffset.UtcNow"/> so
/// the dataset is reproducible across runs.
/// </summary>
public static class YardSeeder
{
    private static readonly DateTimeOffset Anchor = new(2026, 8, 1, 6, 0, 0, TimeSpan.Zero);
    private const string Operator = "T. Verhoeven";

    private static readonly ContainerSize[] SizeCycle =
    [
        ContainerSize.TwentyFoot, ContainerSize.FortyFoot, ContainerSize.FortyFoot, ContainerSize.FortyFiveFoot,
        ContainerSize.TwentyFoot, ContainerSize.FortyFoot, ContainerSize.TwentyFoot, ContainerSize.FortyFiveFoot,
        ContainerSize.FortyFoot, ContainerSize.FortyFoot, ContainerSize.TwentyFoot, ContainerSize.FortyFoot
    ];

    private static readonly ContainerType[] TypeCycle =
    [
        ContainerType.DryVan, ContainerType.DryVan, ContainerType.OpenTop, ContainerType.DryVan,
        ContainerType.DryVan, ContainerType.Reefer, ContainerType.FlatRack, ContainerType.DryVan,
        ContainerType.DryVan, ContainerType.Tank, ContainerType.DryVan, ContainerType.DryVan
    ];

    // Two Expected, three GatedIn, four Stored, two Staged, one GatedOut per line of twelve.
    private static readonly ContainerStatus[] StatusCycle =
    [
        ContainerStatus.Expected, ContainerStatus.Expected,
        ContainerStatus.GatedIn, ContainerStatus.GatedIn, ContainerStatus.GatedIn,
        ContainerStatus.Stored, ContainerStatus.Stored, ContainerStatus.Stored, ContainerStatus.Stored,
        ContainerStatus.Staged, ContainerStatus.Staged,
        ContainerStatus.GatedOut
    ];

    public static void Seed(YardDbContext db)
    {
        if (db.Vessels.Any() || db.Containers.Any())
            return;

        var vessels = CreateVessels();
        db.Vessels.AddRange(vessels);

        var slots = CreateYardSlots();
        db.YardSlots.AddRange(slots);

        var containers = CreateContainers(vessels, slots);
        db.Containers.AddRange(containers);

        db.SaveChanges();
    }

    private static List<Vessel> CreateVessels() =>
    [
        Vessel.Create("MSC Gayane", "9839430", Anchor.AddDays(-6)),
        Vessel.Create("Maersk Sealand", "9784271", Anchor.AddDays(-2)),
        Vessel.Create("CMA CGM Bougainville", "9701694", Anchor.AddDays(3)),
        Vessel.Create("ONE Competence", "9305811", Anchor.AddDays(7))
    ];

    private static List<YardSlot> CreateYardSlots()
    {
        var slots = new List<YardSlot>();

        foreach (var block in new[] { "A", "B", "C", "D" })
        {
            for (var row = 1; row <= 10; row++)
            {
                var isReeferBlock = block == "A";
                var maxTeu = row % 5 == 0 ? 4 : 2;
                slots.Add(YardSlot.Create(block, row, tier: 1, maxTeu, isReeferBlock));
            }
        }

        return slots;
    }

    private static List<Container> CreateContainers(IReadOnlyList<Vessel> vessels, IReadOnlyList<YardSlot> slots)
    {
        var lines = new (string Name, string OwnerPrefix, Vessel? Vessel)[]
        {
            ("MSC", "MSCU", vessels[0]),
            ("Maersk", "MAEU", vessels[1]),
            ("CMA CGM", "CMAU", vessels[2]),
            ("Hapag-Lloyd", "HLXU", null),
            ("ONE", "ONEU", vessels[3])
        };

        var containers = new List<Container>();
        var clock = Anchor;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var (shippingLine, ownerPrefix, vessel) = lines[lineIndex];

            for (var i = 0; i < 12; i++)
            {
                clock = clock.AddHours(3);

                var containerNumber = BuildContainerNumber(ownerPrefix, i + 1);
                var size = SizeCycle[i];
                var type = TypeCycle[i];
                var status = StatusCycle[i];
                var grossWeightKg = GrossWeightFor(size, lineIndex, i);

                var container = Container.Register(
                    containerNumber, size, type, grossWeightKg, shippingLine,
                    i < 6 ? vessel : null);

                if (status == ContainerStatus.Expected)
                {
                    containers.Add(container);
                    continue;
                }

                container.GateIn(clock, Operator);
                clock = clock.AddHours(1);

                if (status == ContainerStatus.GatedIn)
                {
                    containers.Add(container);
                    continue;
                }

                // Two containers demonstrate direct transhipment: GatedIn -> Staged, never yarded.
                var isTranshipment = status == ContainerStatus.Staged && i == 10 && lineIndex is 1 or 3;

                if (!isTranshipment)
                {
                    var slot = AllocateSlot(container.Teu, type == ContainerType.Reefer, slots);
                    container.AssignToSlot(slot, clock, Operator);
                    clock = clock.AddHours(2);
                }

                if (status == ContainerStatus.Stored)
                {
                    if (i == 6 && lineIndex == 4)
                    {
                        container.PlaceHold("Random customs inspection", clock);
                        clock = clock.AddHours(1);
                    }

                    containers.Add(container);
                    continue;
                }

                container.Stage(clock, Operator);
                clock = clock.AddDays(1);

                if (status == ContainerStatus.Staged)
                {
                    if (i == 9 && lineIndex is 1 or 3)
                    {
                        container.PlaceHold("Awaiting customs release", clock);
                        clock = clock.AddHours(2);
                    }

                    containers.Add(container);
                    continue;
                }

                if (lineIndex is 0 or 2)
                {
                    var hold = container.PlaceHold("Documentation verification", clock);
                    clock = clock.AddHours(4);
                    hold.Release(clock);
                    clock = clock.AddHours(1);
                }

                container.GateOut(clock, Operator);
                clock = clock.AddHours(1);

                containers.Add(container);
            }
        }

        return containers;
    }

    private static YardSlot AllocateSlot(int teu, bool needsReefer, IReadOnlyList<YardSlot> slots)
    {
        var candidates = needsReefer ? slots.Where(s => s.IsReeferCapable) : slots.Where(s => !s.IsReeferCapable);
        return candidates.FirstOrDefault(s => s.RemainingTeu >= teu)
            ?? throw new InvalidOperationException("Seed data ran out of yard capacity for the requested slot type.");
    }

    private static string BuildContainerNumber(string ownerPrefix, int serial)
    {
        var prefix = ownerPrefix + serial.ToString("D6");
        var checkDigit = Iso6346.ComputeCheckDigit(prefix)
            ?? throw new InvalidOperationException($"Could not compute an ISO 6346 check digit for '{prefix}'.");
        return prefix + checkDigit;
    }

    private static int GrossWeightFor(ContainerSize size, int lineIndex, int i)
    {
        var baseWeight = size switch
        {
            ContainerSize.TwentyFoot => 8000,
            ContainerSize.FortyFoot => 18000,
            _ => 20000
        };

        return baseWeight + (lineIndex * 12 + i) * 173 % 6000;
    }
}
