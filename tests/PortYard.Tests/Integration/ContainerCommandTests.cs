using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PortYard.Api.Contracts.Containers;
using PortYard.Domain.Enums;
using Xunit;

namespace PortYard.Tests.Integration;

/// <summary>
/// Mutating operations against the seeded database. Each test uses its own container numbers
/// (never asserting on database-wide totals), so it's safe for them to share one database/factory
/// per xUnit's usual "run methods in a class against one fixture instance" model.
/// </summary>
public class ContainerCommandTests(PortYardApiFactory factory) : IClassFixture<PortYardApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostContainer_with_invalid_iso6346_number_returns_400_with_a_useful_message()
    {
        var response = await _client.PostAsJsonAsync("/api/containers", new
        {
            containerNumber = "NOTAREALNUMBER",
            size = "FortyFoot",
            type = "DryVan",
            grossWeightKg = 12000,
            shippingLine = "MSC"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ISO 6346");
    }

    [Fact]
    public async Task PostContainer_with_duplicate_number_returns_409()
    {
        var request = new
        {
            containerNumber = "TESU9000020",
            size = "TwentyFoot",
            type = ContainerType.DryVan,
            grossWeightKg = 9000,
            shippingLine = "MSC"
        };

        var first = await _client.PostAsJsonAsync("/api/containers", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/containers", request);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Full_lifecycle_register_to_gate_out_updates_status_and_movements_at_each_step()
    {
        const string containerNumber = "TESU9000014";

        var register = await _client.PostAsJsonAsync("/api/containers", new
        {
            containerNumber,
            size = "TwentyFoot",
            type = "DryVan",
            grossWeightKg = 9000,
            shippingLine = "MSC"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        (await register.Content.ReadFromJsonAsync<ContainerSummaryDto>(JsonOptions))!.Status.Should().Be(ContainerStatus.Expected);

        var gateIn = await _client.PostAsync($"/api/containers/{containerNumber}/gate-in", null);
        gateIn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await gateIn.Content.ReadFromJsonAsync<ContainerSummaryDto>(JsonOptions))!.Status.Should().Be(ContainerStatus.GatedIn);

        var assignSlot = await _client.PostAsJsonAsync($"/api/containers/{containerNumber}/assign-slot", new { slotCode = "D02-1" });
        assignSlot.StatusCode.Should().Be(HttpStatusCode.OK);
        (await assignSlot.Content.ReadFromJsonAsync<ContainerSummaryDto>(JsonOptions))!.Status.Should().Be(ContainerStatus.Stored);

        var stage = await _client.PostAsync($"/api/containers/{containerNumber}/stage", null);
        stage.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stage.Content.ReadFromJsonAsync<ContainerSummaryDto>(JsonOptions))!.Status.Should().Be(ContainerStatus.Staged);

        var gateOut = await _client.PostAsync($"/api/containers/{containerNumber}/gate-out", null);
        gateOut.StatusCode.Should().Be(HttpStatusCode.OK);
        (await gateOut.Content.ReadFromJsonAsync<ContainerSummaryDto>(JsonOptions))!.Status.Should().Be(ContainerStatus.GatedOut);

        var detail = await _client.GetFromJsonAsync<ContainerDetailDto>($"/api/containers/{containerNumber}", JsonOptions);
        detail!.Movements.Should().HaveCount(4);
        detail.Movements.Select(m => m.Type).Should().Equal(
            MovementType.GateIn, MovementType.Yard, MovementType.Stage, MovementType.GateOut);
    }

    [Fact]
    public async Task GateOut_under_an_active_hold_returns_409_with_a_problem_details_body()
    {
        const string containerNumber = "TESU9000035";

        await _client.PostAsJsonAsync("/api/containers", new
        {
            containerNumber,
            size = "TwentyFoot",
            type = "DryVan",
            grossWeightKg = 9000,
            shippingLine = "MSC"
        });
        await _client.PostAsync($"/api/containers/{containerNumber}/gate-in", null);
        await _client.PostAsync($"/api/containers/{containerNumber}/stage", null);
        await _client.PostAsJsonAsync($"/api/containers/{containerNumber}/holds", new { reason = "Spot check" });

        var response = await _client.PostAsync($"/api/containers/{containerNumber}/gate-out", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        problem.Detail.Should().Contain("customs hold");
    }

    [Fact]
    public async Task Assigning_a_container_to_a_full_slot_returns_409()
    {
        // D01-1 is empty at the start of every fresh seed and has MaxTeu = 2.
        await RegisterGateInAndAssign("TESU9000040", "D01-1"); // fills the slot to exactly 2/2 TEU

        await _client.PostAsJsonAsync("/api/containers", new
        {
            containerNumber = "TESU9000056",
            size = "TwentyFoot",
            type = "DryVan",
            grossWeightKg = 9000,
            shippingLine = "MSC"
        });
        await _client.PostAsync("/api/containers/TESU9000056/gate-in", null);

        var response = await _client.PostAsJsonAsync("/api/containers/TESU9000056/assign-slot", new { slotCode = "D01-1" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task RegisterGateInAndAssign(string containerNumber, string slotCode)
    {
        await _client.PostAsJsonAsync("/api/containers", new
        {
            containerNumber,
            size = "FortyFoot",
            type = "DryVan",
            grossWeightKg = 18000,
            shippingLine = "MSC"
        });
        await _client.PostAsync($"/api/containers/{containerNumber}/gate-in", null);
        await _client.PostAsJsonAsync($"/api/containers/{containerNumber}/assign-slot", new { slotCode });
    }
}
