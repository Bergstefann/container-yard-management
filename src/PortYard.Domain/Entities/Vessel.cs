using PortYard.Domain.Exceptions;

namespace PortYard.Domain.Entities;

/// <summary>A vessel calling at the terminal, with containers pre-advised as inbound on it.</summary>
public class Vessel
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>IMO ship identification number — exactly 7 digits.</summary>
    public string Imo { get; private set; } = string.Empty;

    public DateTimeOffset Eta { get; private set; }

    private readonly List<Container> _inboundContainers = [];
    public IReadOnlyCollection<Container> InboundContainers => _inboundContainers.AsReadOnly();

    private Vessel()
    {
    }

    public static Vessel Create(string name, string imo, DateTimeOffset eta)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Vessel name is required.");

        if (imo is null || imo.Length != 7 || !imo.All(char.IsAsciiDigit))
            throw new DomainRuleException("IMO number must be exactly 7 digits.");

        return new Vessel
        {
            Name = name.Trim(),
            Imo = imo,
            Eta = eta
        };
    }
}
