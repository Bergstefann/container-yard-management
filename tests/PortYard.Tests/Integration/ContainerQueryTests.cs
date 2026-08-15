using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PortYard.Api.Contracts.Common;
using PortYard.Api.Contracts.Containers;
using PortYard.Domain.Enums;
using Xunit;

namespace PortYard.Tests.Integration;

/// <summary>
/// Pure read-only queries against the seeded database. Kept in their own class (and so their
/// own <see cref="PortYardApiFactory"/>/database) so that exact-count assertions can't be
/// thrown off by containers the mutating tests in <see cref="ContainerCommandTests"/> register.
/// </summary>
public class ContainerQueryTests(PortYardApiFactory factory) : IClassFixture<PortYardApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetContainers_returns_200_with_correct_pagination_metadata()
    {
        var result = await _client.GetFromJsonAsync<PagedResult<ContainerSummaryDto>>(
            "/api/containers?page=1&pageSize=10", JsonOptions);

        result.Should().NotBeNull();
        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(60); // the seed registers exactly 60 containers
        result.Items.Should().HaveCount(10);
        result.TotalPages.Should().Be(6);
    }

    [Fact]
    public async Task GetContainers_filtered_by_status_returns_only_matching_containers()
    {
        var result = await _client.GetFromJsonAsync<PagedResult<ContainerSummaryDto>>(
            "/api/containers?status=GatedOut&page=1&pageSize=50", JsonOptions);

        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(5); // exactly one GatedOut container per shipping line in the seed
        result.Items.Should().OnlyContain(c => c.Status == ContainerStatus.GatedOut);
    }

    [Fact]
    public async Task GetContainer_with_unknown_number_returns_404()
    {
        var response = await _client.GetAsync("/api/containers/NOSUCHNUM12");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
