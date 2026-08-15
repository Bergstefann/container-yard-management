using Microsoft.EntityFrameworkCore;
using PortYard.Api.Contracts.Reports;
using PortYard.Api.Data;
using PortYard.Domain.Enums;

namespace PortYard.Api.Services;

public class ReportService(YardDbContext db)
{
    public async Task<IReadOnlyList<YardUtilisationDto>> GetYardUtilisationAsync(CancellationToken ct)
    {
        // Two grouped, aggregate queries do the real work in SQL:
        //   SELECT CurrentSlotId, Block, SUM(CASE WHEN Size='TwentyFoot' THEN 1 ELSE 2 END) AS TeuUsed
        //     FROM Containers JOIN YardSlots ON ... WHERE CurrentSlotId IS NOT NULL
        //     GROUP BY CurrentSlotId, Block
        //   SELECT Block, COUNT(*) AS SlotsTotal, SUM(MaxTeu) AS TeuCapacity
        //     FROM YardSlots GROUP BY Block
        // Only the two small, already-aggregated result sets (at most one row per occupied slot,
        // one row per block) are combined afterwards — that final rollup, not row-level
        // aggregation, is the only part that runs in memory.
        var perSlotOccupancy = await db.Containers
            .Where(c => c.CurrentSlotId != null)
            .GroupBy(c => new { c.CurrentSlotId, c.CurrentSlot!.Block })
            .Select(g => new
            {
                g.Key.Block,
                TeuUsed = g.Sum(c => c.Size == ContainerSize.TwentyFoot ? 1 : 2)
            })
            .ToListAsync(ct);

        var capacityByBlock = await db.YardSlots
            .GroupBy(s => s.Block)
            .Select(g => new
            {
                Block = g.Key,
                SlotsTotal = g.Count(),
                TeuCapacity = g.Sum(s => s.MaxTeu)
            })
            .ToListAsync(ct);

        var occupancyByBlock = perSlotOccupancy
            .GroupBy(o => o.Block)
            .ToDictionary(g => g.Key, g => new { SlotsUsed = g.Count(), TeuUsed = g.Sum(o => o.TeuUsed) });

        return capacityByBlock
            .OrderBy(c => c.Block)
            .Select(c =>
            {
                occupancyByBlock.TryGetValue(c.Block, out var occupancy);
                var teuUsed = occupancy?.TeuUsed ?? 0;

                return new YardUtilisationDto
                {
                    Block = c.Block,
                    SlotsUsed = occupancy?.SlotsUsed ?? 0,
                    SlotsTotal = c.SlotsTotal,
                    TeuUsed = teuUsed,
                    TeuCapacity = c.TeuCapacity,
                    UtilisationPercentage = c.TeuCapacity == 0 ? 0 : Math.Round(teuUsed * 100.0 / c.TeuCapacity, 1)
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DwellTimeReportEntryDto>> GetDwellTimeReportAsync(CancellationToken ct)
    {
        // Filtering (departed only) and projection (four narrow columns) run in SQL:
        //   SELECT ShippingLine, Type, ArrivedAt, DepartedAt FROM Containers WHERE Status = 'GatedOut'
        // SQLite has no MEDIAN aggregate, and EF Core's Sqlite provider does not reliably
        // translate DateTimeOffset subtraction, so the dwell-hours computation, grouping, and
        // average/median are done in memory over this already-filtered, already-narrow result
        // set rather than pulling the full container graph.
        var departed = await db.Containers
            .Where(c => c.Status == ContainerStatus.GatedOut && c.ArrivedAt != null && c.DepartedAt != null)
            .Select(c => new { c.ShippingLine, c.Type, c.ArrivedAt, c.DepartedAt })
            .ToListAsync(ct);

        return departed
            .Select(c => new
            {
                c.ShippingLine,
                c.Type,
                DwellHours = (c.DepartedAt!.Value - c.ArrivedAt!.Value).TotalHours
            })
            .GroupBy(c => new { c.ShippingLine, c.Type })
            .Select(g =>
            {
                var hours = g.Select(x => x.DwellHours).OrderBy(h => h).ToList();
                return new DwellTimeReportEntryDto
                {
                    ShippingLine = g.Key.ShippingLine,
                    ContainerType = g.Key.Type,
                    AverageDwellHours = Math.Round(hours.Average(), 1),
                    MedianDwellHours = Math.Round(Median(hours), 1),
                    SampleSize = hours.Count
                };
            })
            .OrderBy(e => e.ShippingLine).ThenBy(e => e.ContainerType)
            .ToList();
    }

    public async Task<IReadOnlyList<ThroughputReportEntryDto>> GetThroughputReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // The date-range filter and column projection run in SQL:
        //   SELECT Type, OccurredAt FROM Movements WHERE Type IN ('GateIn','GateOut')
        // EF Core's Sqlite provider cannot translate a DateTimeOffset range comparison combined
        // with an enum filter in a single predicate, so the date-range bound and the bucketing
        // by calendar day happen afterwards, in memory, over this Type-filtered (not full-table)
        // result set.
        var gateMovements = await db.Movements
            .Where(m => m.Type == MovementType.GateIn || m.Type == MovementType.GateOut)
            .Select(m => new { m.Type, m.OccurredAt })
            .ToListAsync(ct);

        return gateMovements
            .Where(m => m.OccurredAt >= from && m.OccurredAt <= to)
            .GroupBy(m => DateOnly.FromDateTime(m.OccurredAt.UtcDateTime.Date))
            .Select(g => new ThroughputReportEntryDto
            {
                Date = g.Key,
                GateInCount = g.Count(m => m.Type == MovementType.GateIn),
                GateOutCount = g.Count(m => m.Type == MovementType.GateOut)
            })
            .OrderBy(e => e.Date)
            .ToList();
    }

    private static double Median(IReadOnlyList<double> sortedValues)
    {
        var count = sortedValues.Count;
        if (count == 0)
            return 0;

        var mid = count / 2;
        return count % 2 == 0
            ? (sortedValues[mid - 1] + sortedValues[mid]) / 2.0
            : sortedValues[mid];
    }
}
