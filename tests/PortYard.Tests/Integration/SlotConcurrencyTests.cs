using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortYard.Api.Data;
using Xunit;

namespace PortYard.Tests.Integration;

/// <summary>
/// The in-request capacity check on its own can't close this race: two assign-slot calls can
/// each read the same slot before either one writes, each individually pass the capacity check
/// against that snapshot, and both commit — over-filling the slot past MaxTeu. These tests
/// simulate that deterministically (two separate DbContexts, each fully loading its own
/// snapshot before either one saves) rather than relying on real thread timing, which would
/// make the test flaky and non-repeatable.
///
/// The race is exercised via the domain method directly (Container.AssignToSlot), not through
/// ContainerService: the service always re-queries with a fresh Include on every call, so by the
/// time a second service call runs after the first has already saved, its own capacity check
/// sees current — not stale — occupancy and rejects it there instead. That's a fine outcome in
/// practice, but it means a fully sequential test can't force the service itself through the
/// narrow window where the concurrency token is what has to catch it; only a genuinely
/// concurrent pair of in-flight requests can. This test proves the mechanism that protects that
/// window works: a save against a since-changed slot fails instead of silently landing.
/// </summary>
public class SlotConcurrencyTests(PortYardApiFactory factory) : IClassFixture<PortYardApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Second_of_two_concurrent_assigns_that_would_overfill_a_slot_fails_the_write()
    {
        // D03-1: untouched by any other test in this suite, MaxTeu = 2 (row 3, not a multiple of 5).
        const string containerA = "TESU0000712";
        const string containerB = "TESU0000728";

        // Each container is a 40-footer (Teu = 2) — each fits D03-1's MaxTeu = 2 on its own,
        // read against an empty slot, but the two together (4 Teu) would overfill it by 2.
        await RegisterAndGateIn(containerA);
        await RegisterAndGateIn(containerB);

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<YardDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<YardDbContext>();

        // Both "requests" fully load what the capacity check needs — including the occupancy
        // navigation — while the slot is still genuinely empty, before either one writes
        // anything back. Loading Containers explicitly here (not relying on a later query to
        // fill it in) is what keeps dbB's copy stale after dbA commits.
        var slotA = await dbA.YardSlots.Include(s => s.Containers)
            .SingleAsync(s => s.Block == "D" && s.Row == 3 && s.Tier == 1);
        var loadedContainerA = await dbA.Containers.SingleAsync(c => c.ContainerNumber == containerA);
        var slotB = await dbB.YardSlots.Include(s => s.Containers)
            .SingleAsync(s => s.Block == "D" && s.Row == 3 && s.Tier == 1);
        var loadedContainerB = await dbB.Containers.SingleAsync(c => c.ContainerNumber == containerB);

        // First writer: the capacity check is genuinely correct here (the slot really is
        // empty), and its save bumps the slot's concurrency token.
        loadedContainerA.AssignToSlot(slotA, DateTimeOffset.UtcNow, "gate-system");
        await dbA.SaveChangesAsync();

        // Second writer: still holds the pre-save slot snapshot, so its capacity check also
        // (wrongly, in isolation) says "fits" — this is exactly the race the README describes.
        // The save is what has to catch it.
        loadedContainerB.AssignToSlot(slotB, DateTimeOffset.UtcNow, "gate-system");
        var act = async () => await dbB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // The slot never actually went over capacity — containerB's write never landed.
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<YardDbContext>();
        var finalSlot = await verifyDb.YardSlots.Include(s => s.Containers)
            .SingleAsync(s => s.Block == "D" && s.Row == 3 && s.Tier == 1);
        finalSlot.UsedTeu.Should().Be(2);
    }

    private async Task RegisterAndGateIn(string containerNumber)
    {
        var register = await _client.PostAsJsonAsync("/api/containers", new
        {
            containerNumber,
            size = "FortyFoot",
            type = "DryVan",
            grossWeightKg = 18000,
            shippingLine = "MSC"
        });
        register.EnsureSuccessStatusCode();

        var gateIn = await _client.PostAsync($"/api/containers/{containerNumber}/gate-in", null);
        gateIn.EnsureSuccessStatusCode();
    }
}
