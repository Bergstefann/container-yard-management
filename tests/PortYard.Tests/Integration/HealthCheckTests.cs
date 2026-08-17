using System.Net;
using FluentAssertions;
using Xunit;

namespace PortYard.Tests.Integration;

public class HealthCheckTests(PortYardApiFactory factory) : IClassFixture<PortYardApiFactory>
{
    [Fact]
    public async Task Health_endpoint_returns_200_when_the_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
