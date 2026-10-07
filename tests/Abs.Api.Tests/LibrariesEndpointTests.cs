using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abs.Api.Abstractions;
using Abs.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Abs.Api.Tests;

public class LibrariesEndpointTests : IDisposable
{
    private const string DevHeader = "X-Dev-User";

    private readonly AbsApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private HttpClient ClientFor(string? userId, AbsApiFactory? custom = null)
    {
        var client = (custom ?? factory).CreateClient();
        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(DevHeader, userId);
        }

        return client;
    }

    private static JsonNode NodeLibraries() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "node-libraries.json")))!;

    // The expected body was produced by the real audiobookshelf 2.37.1 code (Library.toOldJSON)
    // running on a copy of the database. See docs/research/k2-shape-comparison.md.
    [Fact]
    public async Task GetAll_for_a_user_limited_to_A_matches_Node_response_exactly()
    {
        var response = await ClientFor("user-only-a").GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(JsonNode.DeepEquals(NodeLibraries(), actual), $"Actual: {actual.ToJsonString()}");
    }

    [Fact]
    public async Task GetAll_keeps_the_property_order_of_Node()
    {
        var response = await ClientFor("user-only-a").GetAsync("/api/libraries");

        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["libraries"]![0]!.AsObject();
        var expected = NodeLibraries()["libraries"]![0]!.AsObject();
        Assert.Equal(expected.Select(x => x.Key), actual.Select(x => x.Key));
    }

    [Fact]
    public async Task GetById_matches_the_library_object_of_Node()
    {
        var response = await ClientFor("user-admin").GetAsync($"/api/libraries/{AbsApiFactory.LibraryA}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(JsonNode.DeepEquals(NodeLibraries()["libraries"]![0], actual), $"Actual: {actual.ToJsonString()}");
    }

    [Fact]
    public async Task GetAll_for_admin_returns_libraries_ordered_by_displayOrder()
    {
        var response = await ClientFor("user-admin").GetAsync("/api/libraries");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("libraries").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToList();
        Assert.Equal([AbsApiFactory.LibraryA, AbsApiFactory.LibraryB], ids);
    }

    [Fact]
    public async Task Library_without_scan_settings_or_folders_uses_nulls_and_empty_values()
    {
        var response = await ClientFor("user-admin").GetAsync($"/api/libraries/{AbsApiFactory.LibraryB}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastScan").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastScanVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("provider").ValueKind);
        Assert.Empty(body.GetProperty("settings").EnumerateObject());
        Assert.Empty(body.GetProperty("folders").EnumerateArray());
        Assert.Equal(DateTimeOffset.Parse("2026-10-07T01:00:00Z").ToUnixTimeMilliseconds(), body.GetProperty("createdAt").GetInt64());
    }

    [Fact]
    public async Task GetAll_filters_by_the_library_list_of_the_user()
    {
        var response = await ClientFor("user-only-b").GetAsync("/api/libraries");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("libraries").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToList();
        Assert.Equal([AbsApiFactory.LibraryB], ids);
    }

    // Node quirk (LibraryController.js:191-194): the list endpoint filters on a non-empty list
    // even when accessAllLibraries is true, while the single-library check honours the flag.
    [Fact]
    public async Task GetAll_applies_a_non_empty_list_even_when_accessAllLibraries_is_true()
    {
        var list = await ClientFor("user-quirk").GetAsync("/api/libraries");
        var single = await ClientFor("user-quirk").GetAsync($"/api/libraries/{AbsApiFactory.LibraryA}");

        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(body.GetProperty("libraries").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task GetById_returns_403_when_the_user_cannot_access_the_library()
    {
        var response = await ClientFor("user-only-b").GetAsync($"/api/libraries/{AbsApiFactory.LibraryA}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_returns_404_for_an_unknown_library_when_the_user_can_access_all()
    {
        var response = await ClientFor("user-admin").GetAsync("/api/libraries/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Library not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_user_without_permissions_cannot_access_a_library()
    {
        var response = await ClientFor("user-no-perms").GetAsync($"/api/libraries/{AbsApiFactory.LibraryA}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_user_gets_403()
    {
        var response = await ClientFor("nobody").GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_include_values_return_501_so_Node_can_handle_them()
    {
        var stats = await ClientFor("user-admin").GetAsync("/api/libraries?include=stats");
        var filterData = await ClientFor("user-admin").GetAsync($"/api/libraries/{AbsApiFactory.LibraryA}?include=filterdata");

        Assert.Equal(HttpStatusCode.NotImplemented, stats.StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, filterData.StatusCode);
    }

    [Fact]
    public async Task The_controller_asks_IPermissionService_instead_of_reading_permissions_itself()
    {
        using var custom = new AbsApiFactory
        {
            ConfigureTestServices = services =>
                services.Replace(ServiceDescriptor.Scoped<IPermissionService>(_ => new FakePermissionService(false, []))),
        };

        var response = await ClientFor("user-admin", custom).GetAsync($"/api/libraries/{AbsApiFactory.LibraryA}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Fake_progress_reader_returns_what_it_was_given()
    {
        IUserProgressReader reader = new FakeUserProgressReader().Add("user-admin", new UserProgress("item-1", 0.5, 120, false));

        Assert.Equal(0.5, (await reader.GetProgressAsync("user-admin", "item-1"))!.Progress);
        Assert.Null(await reader.GetProgressAsync("user-admin", "item-2"));
    }
}

public class DevLoginTests
{
    [Fact]
    public async Task Requests_without_the_dev_header_are_unauthorized()
    {
        using var factory = new AbsApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task The_dev_header_is_ignored_outside_Development(string environment)
    {
        using var factory = new AbsApiFactory { EnvironmentName = environment };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "user-admin");

        var response = await client.GetAsync("/api/libraries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
