namespace PortYard.Api.Contracts.Reports;

public class YardUtilisationDto
{
    public required string Block { get; init; }
    public required int SlotsUsed { get; init; }
    public required int SlotsTotal { get; init; }
    public required int TeuUsed { get; init; }
    public required int TeuCapacity { get; init; }
    public required double UtilisationPercentage { get; init; }
}
