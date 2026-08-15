using Microsoft.EntityFrameworkCore;
using PortYard.Api.Contracts.CustomsHolds;
using PortYard.Api.Data;
using PortYard.Domain.Entities;
using PortYard.Domain.Exceptions;

namespace PortYard.Api.Services;

public class CustomsHoldService(YardDbContext db)
{
    public async Task<CustomsHoldDto> PlaceHoldAsync(string containerNumber, string reason, CancellationToken ct)
    {
        var normalized = containerNumber.Trim().ToUpperInvariant();

        var container = await db.Containers
            .Include(c => c.CustomsHolds)
            .FirstOrDefaultAsync(c => c.ContainerNumber == normalized, ct)
            ?? throw new EntityNotFoundException($"No container found with number '{normalized}'.");

        var hold = container.PlaceHold(reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        return ToDto(hold, container.ContainerNumber);
    }

    public async Task<CustomsHoldDto> ReleaseHoldAsync(int holdId, CancellationToken ct)
    {
        var hold = await db.CustomsHolds
            .Include(h => h.Container)
            .FirstOrDefaultAsync(h => h.Id == holdId, ct)
            ?? throw new EntityNotFoundException($"No customs hold found with id {holdId}.");

        hold.Release(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        return ToDto(hold, hold.Container.ContainerNumber);
    }

    private static CustomsHoldDto ToDto(CustomsHold hold, string containerNumber) => new()
    {
        Id = hold.Id,
        ContainerNumber = containerNumber,
        Reason = hold.Reason,
        PlacedAt = hold.PlacedAt,
        ReleasedAt = hold.ReleasedAt,
        IsActive = hold.IsActive
    };
}
