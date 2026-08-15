using PortYard.Domain.Exceptions;

namespace PortYard.Domain.Entities;

/// <summary>A physical yard location addressed by block/row/tier, with a fixed TEU capacity.</summary>
public class YardSlot
{
    public int Id { get; private set; }
    public string Block { get; private set; } = string.Empty;
    public int Row { get; private set; }
    public int Tier { get; private set; }
    public int MaxTeu { get; private set; }
    public bool IsReeferCapable { get; private set; }

    public string Code => $"{Block}{Row:D2}-{Tier}";

    private readonly List<Container> _containers = [];
    public IReadOnlyCollection<Container> Containers => _containers.AsReadOnly();

    public int UsedTeu => _containers.Sum(c => c.Teu);
    public int RemainingTeu => MaxTeu - UsedTeu;

    private YardSlot()
    {
    }

    public static YardSlot Create(string block, int row, int tier, int maxTeu, bool isReeferCapable)
    {
        if (string.IsNullOrWhiteSpace(block))
            throw new DomainRuleException("Block is required.");

        if (row <= 0)
            throw new DomainRuleException("Row must be positive.");

        if (tier <= 0)
            throw new DomainRuleException("Tier must be positive.");

        if (maxTeu <= 0)
            throw new DomainRuleException("A slot must have positive TEU capacity.");

        return new YardSlot
        {
            Block = block.Trim().ToUpperInvariant(),
            Row = row,
            Tier = tier,
            MaxTeu = maxTeu,
            IsReeferCapable = isReeferCapable
        };
    }

    /// <summary>Adds a container to this slot's occupancy. Only <see cref="Container"/> calls this — it owns the transition rules.</summary>
    internal void AddContainer(Container container) => _containers.Add(container);

    /// <summary>Removes a container from this slot's occupancy. Only <see cref="Container"/> calls this — it owns the transition rules.</summary>
    internal void RemoveContainer(Container container) => _containers.Remove(container);
}
