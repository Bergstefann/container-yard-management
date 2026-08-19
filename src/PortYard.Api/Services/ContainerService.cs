using Microsoft.EntityFrameworkCore;
using PortYard.Api.Contracts.Common;
using PortYard.Api.Contracts.Containers;
using PortYard.Api.Contracts.CustomsHolds;
using PortYard.Api.Data;
using PortYard.Domain.Entities;
using PortYard.Domain.Enums;
using PortYard.Domain.Exceptions;

namespace PortYard.Api.Services;

public class ContainerService(YardDbContext db)
{
    private const string DefaultOperator = "gate-system";

    public async Task<PagedResult<ContainerSummaryDto>> GetPagedAsync(
        ContainerStatus? status, ContainerSize? size, ContainerType? type, string? shippingLine, string? block,
        int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Containers.AsNoTracking().Include(c => c.CurrentSlot).AsQueryable();

        if (status is not null) query = query.Where(c => c.Status == status);
        if (size is not null) query = query.Where(c => c.Size == size);
        if (type is not null) query = query.Where(c => c.Type == type);
        if (!string.IsNullOrWhiteSpace(shippingLine)) query = query.Where(c => c.ShippingLine == shippingLine);
        if (!string.IsNullOrWhiteSpace(block)) query = query.Where(c => c.CurrentSlot != null && c.CurrentSlot.Block == block);

        var totalCount = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(c => c.ContainerNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.ContainerNumber,
                c.Size,
                c.Type,
                c.Status,
                c.ShippingLine,
                c.GrossWeightKg,
                c.ArrivedAt,
                c.DepartedAt,
                SlotBlock = c.CurrentSlot == null ? null : c.CurrentSlot.Block,
                SlotRow = c.CurrentSlot == null ? (int?)null : c.CurrentSlot.Row,
                SlotTier = c.CurrentSlot == null ? (int?)null : c.CurrentSlot.Tier
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ContainerSummaryDto
        {
            ContainerNumber = r.ContainerNumber,
            Size = r.Size,
            Type = r.Type,
            Status = r.Status,
            ShippingLine = r.ShippingLine,
            GrossWeightKg = r.GrossWeightKg,
            CurrentSlotCode = r.SlotBlock is null ? null : SlotCodeFormatter.Format(r.SlotBlock, r.SlotRow!.Value, r.SlotTier!.Value),
            ArrivedAt = r.ArrivedAt,
            DepartedAt = r.DepartedAt
        }).ToList();

        return new PagedResult<ContainerSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ContainerDetailDto> GetDetailAsync(string containerNumber, CancellationToken ct)
    {
        var normalized = Normalise(containerNumber);

        var container = await db.Containers
            .AsNoTracking()
            .Include(c => c.CurrentSlot)
            .Include(c => c.InboundVessel)
            .Include(c => c.Movements).ThenInclude(m => m.FromSlot)
            .Include(c => c.Movements).ThenInclude(m => m.ToSlot)
            .Include(c => c.CustomsHolds)
            .FirstOrDefaultAsync(c => c.ContainerNumber == normalized, ct)
            ?? throw new EntityNotFoundException($"No container found with number '{normalized}'.");

        var movements = container.Movements
            .OrderBy(m => m.OccurredAt)
            .Select(m => new MovementDto
            {
                Type = m.Type,
                FromSlotCode = m.FromSlot is null ? null : SlotCodeFormatter.Format(m.FromSlot.Block, m.FromSlot.Row, m.FromSlot.Tier),
                ToSlotCode = m.ToSlot is null ? null : SlotCodeFormatter.Format(m.ToSlot.Block, m.ToSlot.Row, m.ToSlot.Tier),
                OccurredAt = m.OccurredAt,
                Operator = m.Operator
            })
            .ToList();

        var activeHolds = container.CustomsHolds
            .Where(h => h.IsActive)
            .Select(h => new CustomsHoldDto
            {
                Id = h.Id,
                ContainerNumber = container.ContainerNumber,
                Reason = h.Reason,
                PlacedAt = h.PlacedAt,
                ReleasedAt = h.ReleasedAt,
                IsActive = h.IsActive
            })
            .ToList();

        return new ContainerDetailDto
        {
            ContainerNumber = container.ContainerNumber,
            Size = container.Size,
            Type = container.Type,
            Status = container.Status,
            ShippingLine = container.ShippingLine,
            GrossWeightKg = container.GrossWeightKg,
            CurrentSlotCode = container.CurrentSlot is null ? null : SlotCodeFormatter.Format(container.CurrentSlot.Block, container.CurrentSlot.Row, container.CurrentSlot.Tier),
            InboundVesselName = container.InboundVessel?.Name,
            ArrivedAt = container.ArrivedAt,
            DepartedAt = container.DepartedAt,
            ActiveHolds = activeHolds,
            Movements = movements
        };
    }

    public async Task<ContainerSummaryDto> RegisterAsync(RegisterContainerRequest request, CancellationToken ct)
    {
        var normalized = Normalise(request.ContainerNumber);

        var exists = await db.Containers.AnyAsync(c => c.ContainerNumber == normalized, ct);
        if (exists)
            throw new DomainRuleException($"A container with number '{normalized}' is already registered.");

        Vessel? vessel = null;
        if (request.InboundVesselId is not null)
        {
            vessel = await db.Vessels.FindAsync([request.InboundVesselId.Value], ct)
                ?? throw new EntityNotFoundException($"No vessel found with id {request.InboundVesselId}.");
        }

        var container = Container.Register(normalized, request.Size, request.Type, request.GrossWeightKg, request.ShippingLine, vessel);

        db.Containers.Add(container);
        await db.SaveChangesAsync(ct);

        return ToSummary(container);
    }

    public async Task<ContainerSummaryDto> GateInAsync(string containerNumber, CancellationToken ct)
    {
        var container = await LoadTrackedAsync(containerNumber, ct);
        container.GateIn(DateTimeOffset.UtcNow, DefaultOperator);
        await db.SaveChangesAsync(ct);
        return ToSummary(container);
    }

    public async Task<ContainerSummaryDto> AssignSlotAsync(string containerNumber, string slotCode, CancellationToken ct)
    {
        var container = await LoadTrackedAsync(containerNumber, ct);
        var slot = await FindSlotByCodeAsync(slotCode, ct);

        container.AssignToSlot(slot, DateTimeOffset.UtcNow, DefaultOperator);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else's assign-slot committed against this same slot between our read and
            // our write — our capacity check ran against a snapshot that's no longer current.
            // Not this request's job to retry blindly; tell the caller so they can re-check and
            // resubmit against the slot's current state.
            throw new DomainRuleException(
                $"Slot {slotCode} was changed by another request while this one was in progress. Retry the assignment.");
        }

        return ToSummary(container);
    }

    public async Task<ContainerSummaryDto> StageAsync(string containerNumber, CancellationToken ct)
    {
        var container = await LoadTrackedAsync(containerNumber, ct);
        container.Stage(DateTimeOffset.UtcNow, DefaultOperator);
        await db.SaveChangesAsync(ct);
        return ToSummary(container);
    }

    public async Task<ContainerSummaryDto> GateOutAsync(string containerNumber, CancellationToken ct)
    {
        var container = await LoadTrackedAsync(containerNumber, ct);
        container.GateOut(DateTimeOffset.UtcNow, DefaultOperator);
        await db.SaveChangesAsync(ct);
        return ToSummary(container);
    }

    private async Task<Container> LoadTrackedAsync(string containerNumber, CancellationToken ct)
    {
        var normalized = Normalise(containerNumber);

        return await db.Containers
            .Include(c => c.CurrentSlot)
                .ThenInclude(s => s!.Containers)
            .Include(c => c.Movements)
            .Include(c => c.CustomsHolds)
            .FirstOrDefaultAsync(c => c.ContainerNumber == normalized, ct)
            ?? throw new EntityNotFoundException($"No container found with number '{normalized}'.");
    }

    /// <summary>Parses a "Block+Row-Tier" code (e.g. "A05-1") and looks the slot up by its raw columns, since Code itself isn't a mapped column.</summary>
    private async Task<YardSlot> FindSlotByCodeAsync(string slotCode, CancellationToken ct)
    {
        var (block, row, tier) = SlotCodeFormatter.Parse(slotCode);

        return await db.YardSlots
            .Include(s => s.Containers)
            .SingleOrDefaultAsync(s => s.Block == block && s.Row == row && s.Tier == tier, ct)
            ?? throw new EntityNotFoundException($"No yard slot found with code '{slotCode}'.");
    }

    private static ContainerSummaryDto ToSummary(Container container) => new()
    {
        ContainerNumber = container.ContainerNumber,
        Size = container.Size,
        Type = container.Type,
        Status = container.Status,
        ShippingLine = container.ShippingLine,
        GrossWeightKg = container.GrossWeightKg,
        CurrentSlotCode = container.CurrentSlot is null
            ? null
            : SlotCodeFormatter.Format(container.CurrentSlot.Block, container.CurrentSlot.Row, container.CurrentSlot.Tier),
        ArrivedAt = container.ArrivedAt,
        DepartedAt = container.DepartedAt
    };

    private static string Normalise(string containerNumber) => containerNumber.Trim().ToUpperInvariant();
}
