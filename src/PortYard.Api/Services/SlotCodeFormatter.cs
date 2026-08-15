using PortYard.Domain.Exceptions;

namespace PortYard.Api.Services;

/// <summary>
/// Formats and parses slot codes ("Block+Row-Tier", e.g. "A05-1") against the raw Block/Row/Tier
/// columns. <see cref="PortYard.Domain.Entities.YardSlot.Code"/> is a computed property, not a
/// mapped column, so it can't be referenced inside an EF LINQ query — callers project the raw
/// columns and use this to format or parse in memory instead.
/// </summary>
internal static class SlotCodeFormatter
{
    public static string Format(string block, int row, int tier) => $"{block}{row:D2}-{tier}";

    public static (string Block, int Row, int Tier) Parse(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var dashIndex = normalized.LastIndexOf('-');

        if (dashIndex < 3 || dashIndex == normalized.Length - 1)
            throw new DomainRuleException($"'{code}' is not a valid slot code.");

        var tierPart = normalized[(dashIndex + 1)..];
        var beforeDash = normalized[..dashIndex];
        var rowPart = beforeDash[^2..];
        var blockPart = beforeDash[..^2];

        if (string.IsNullOrWhiteSpace(blockPart) || !int.TryParse(rowPart, out var row) || !int.TryParse(tierPart, out var tier))
            throw new DomainRuleException($"'{code}' is not a valid slot code.");

        return (blockPart, row, tier);
    }
}
