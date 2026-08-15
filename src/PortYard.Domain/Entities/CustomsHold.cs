using PortYard.Domain.Exceptions;

namespace PortYard.Domain.Entities;

/// <summary>A customs hold placed against a container. While active, the container cannot gate out.</summary>
public class CustomsHold
{
    public int Id { get; private set; }
    public int ContainerId { get; private set; }
    public Container Container { get; private set; } = null!;
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset PlacedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }

    public bool IsActive => ReleasedAt is null;

    private CustomsHold()
    {
    }

    /// <summary>Only <see cref="Container"/> creates holds, so it can track which of its holds are active.</summary>
    internal CustomsHold(Container container, string reason, DateTimeOffset placedAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("A customs hold requires a reason.");

        Container = container;
        Reason = reason.Trim();
        PlacedAt = placedAt;
    }

    public void Release(DateTimeOffset releasedAt)
    {
        if (!IsActive)
            throw new DomainRuleException("This customs hold has already been released.");

        if (releasedAt < PlacedAt)
            throw new DomainRuleException("Release time cannot precede the time the hold was placed.");

        ReleasedAt = releasedAt;
    }
}
