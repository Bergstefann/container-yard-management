using Microsoft.EntityFrameworkCore;
using PortYard.Api.Contracts.Containers;
using PortYard.Api.Contracts.YardSlots;
using PortYard.Api.Data;
using PortYard.Domain.Entities;
using PortYard.Domain.Exceptions;

namespace PortYard.Api.Services;

public class YardSlotService(YardDbContext db)
{
    public async Task<IReadOnlyList<YardSlotSummaryDto>> GetAllAsync(CancellationToken ct)
    {
        var slots = await db.YardSlots
            .AsNoTracking()
            .Include(s => s.Containers)
            .OrderBy(s => s.Block).ThenBy(s => s.Row).ThenBy(s => s.Tier)
            .ToListAsync(ct);

        return slots.Select(ToSummary).ToList();
    }

    public async Task<YardSlotDetailDto> GetDetailAsync(string code, CancellationToken ct)
    {
        var (block, row, tier) = SlotCodeFormatter.Parse(code);

        var slot = await db.YardSlots
            .AsNoTracking()
            .Include(s => s.Containers)
            .SingleOrDefaultAsync(s => s.Block == block && s.Row == row && s.Tier == tier, ct)
            ?? throw new EntityNotFoundException($"No yard slot found with code '{code}'.");

        var slotCode = SlotCodeFormatter.Format(slot.Block, slot.Row, slot.Tier);

        return new YardSlotDetailDto
        {
            Code = slotCode,
            Block = slot.Block,
            Row = slot.Row,
            Tier = slot.Tier,
            MaxTeu = slot.MaxTeu,
            UsedTeu = slot.UsedTeu,
            RemainingTeu = slot.RemainingTeu,
            IsReeferCapable = slot.IsReeferCapable,
            Containers = slot.Containers.Select(c => new ContainerSummaryDto
            {
                ContainerNumber = c.ContainerNumber,
                Size = c.Size,
                Type = c.Type,
                Status = c.Status,
                ShippingLine = c.ShippingLine,
                GrossWeightKg = c.GrossWeightKg,
                CurrentSlotCode = slotCode,
                ArrivedAt = c.ArrivedAt,
                DepartedAt = c.DepartedAt
            }).ToList()
        };
    }

    private static YardSlotSummaryDto ToSummary(YardSlot slot) => new()
    {
        Code = SlotCodeFormatter.Format(slot.Block, slot.Row, slot.Tier),
        Block = slot.Block,
        Row = slot.Row,
        Tier = slot.Tier,
        MaxTeu = slot.MaxTeu,
        UsedTeu = slot.UsedTeu,
        RemainingTeu = slot.RemainingTeu,
        IsReeferCapable = slot.IsReeferCapable
    };
}
