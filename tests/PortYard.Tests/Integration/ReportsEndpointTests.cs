using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PortYard.Api.Contracts.Reports;
using PortYard.Domain.Enums;
using Xunit;

namespace PortYard.Tests.Integration;

public class ReportsEndpointTests(PortYardApiFactory factory) : IClassFixture<PortYardApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetYardUtilisation_returns_correct_figures_against_the_known_seeded_state()
    {
        var report = (await _client.GetFromJsonAsync<List<YardUtilisationDto>>("/api/reports/yard-utilisation", JsonOptions))!;

        report.Should().NotBeNull();
        report.Should().HaveCount(4); // blocks A-D
        report.Sum(b => b.SlotsTotal).Should().Be(40);
        report.Sum(b => b.TeuCapacity).Should().Be(96);

        // 5 shipping lines x 1 stored reefer container each (40ft = 2 TEU), and reefers are only
        // ever placed in block A (the only reefer-capable block) — see YardSeeder/SlotCapacityTests.
        var blockA = report.Single(b => b.Block == "A");
        blockA.SlotsUsed.Should().Be(5);
        blockA.TeuUsed.Should().Be(10);
        blockA.SlotsTotal.Should().Be(10);
        blockA.TeuCapacity.Should().Be(24);

        // 5 lines x (1 TEU + 2 TEU + 2 TEU) of non-reefer stored containers = 25 TEU, plus block
        // A's 10 TEU of reefers, is every TEU the seed currently has stored anywhere in the yard.
        report.Sum(b => b.TeuUsed).Should().Be(35);

        report.Should().OnlyContain(b => b.UtilisationPercentage == Math.Round(b.TeuUsed * 100.0 / b.TeuCapacity, 1));
    }

    [Fact]
    public async Task GetDwellTime_returns_correct_averages_against_the_known_seeded_state()
    {
        var report = (await _client.GetFromJsonAsync<List<DwellTimeReportEntryDto>>("/api/reports/dwell-time", JsonOptions))!;

        report.Should().NotBeNull();

        // Exactly one GatedOut (DryVan) container per shipping line in the seed, so each group
        // has a sample size of one and its average trivially equals its median.
        report.Should().HaveCount(5);
        report.Should().OnlyContain(e => e.ContainerType == ContainerType.DryVan);
        report.Should().OnlyContain(e => e.SampleSize == 1);
        report.Should().OnlyContain(e => e.AverageDwellHours == e.MedianDwellHours && e.AverageDwellHours > 0);
        report.Select(e => e.ShippingLine).Should().BeEquivalentTo(
            "MSC", "Maersk", "CMA CGM", "Hapag-Lloyd", "ONE");
    }
}
