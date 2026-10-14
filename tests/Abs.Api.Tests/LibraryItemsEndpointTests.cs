using System.Net;
using System.Text.Json.Nodes;
using Abs.Api.Abstractions;
using Abs.Api.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Abs.Api.Tests;

public class LibraryItemsEndpointTests : IDisposable
{
    private const string DevHeader = "X-Dev-User";
    private const string Hobbit = "li-hobbit";
    private const string Dune = "li-dune";
    private const string Omens = "li-omens";
    private const string Alpha = "li-alpha";
    private const string Podcast = "li-podcast";

    private readonly AbsApiFactory factory = new() { IncludeW2Fixtures = false };

    public void Dispose() => factory.Dispose();

    // node-items.json is produced by tests/tools/record-node-items.js, which runs audiobookshelf's own
    // model code (toOldJSON, getByFilterAndSort) on an in-memory database holding the same rows as seed.sql.
    private static readonly JsonNode NodeItems =
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "node-items.json")))!;

    public static TheoryData<string> ListScenarios()
    {
        var data = new TheoryData<string>();
        foreach (var (name, _) in NodeItems["lists"]!.AsObject())
        {
            data.Add(name);
        }

        return data;
    }

    private HttpClient ClientFor(string? userId, AbsApiFactory? custom = null)
    {
        var client = (custom ?? factory).CreateClient();
        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(DevHeader, userId);
        }

        return client;
    }

    private static async Task<JsonNode> BodyOf(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static string ListUrl(string libraryId, string query = "") => $"/api/libraries/{libraryId}/items{query}";

    // GET /api/items/:id

    [Theory]
    [InlineData(Hobbit)]
    [InlineData(Dune)]
    [InlineData(Omens)]
    [InlineData(Alpha)]
    [InlineData("li-cabbage")]
    [InlineData("li-empty")]
    public async Task GetItem_matches_the_Node_response_exactly(string itemId)
    {
        var response = await ClientFor("user-full").GetAsync($"/api/items/{itemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await BodyOf(response);
        Assert.True(JsonNode.DeepEquals(NodeItems["items"]![itemId], actual), $"Actual: {actual.ToJsonString()}");
    }

    [Fact]
    public async Task GetItem_keeps_the_property_order_of_Node()
    {
        var response = await ClientFor("user-full").GetAsync($"/api/items/{Hobbit}");

        var actual = (await BodyOf(response)).AsObject();
        var expected = NodeItems["items"]![Hobbit]!.AsObject();
        Assert.Equal(expected.Select(x => x.Key), actual.Select(x => x.Key));
        Assert.Equal(expected["media"]!.AsObject().Select(x => x.Key), actual["media"]!.AsObject().Select(x => x.Key));
    }

    [Fact]
    public async Task GetItem_returns_404_for_an_unknown_id()
    {
        var response = await ClientFor("user-full").GetAsync("/api/items/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not Found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetItem_returns_401_without_authentication()
    {
        var response = await ClientFor(null).GetAsync($"/api/items/{Hobbit}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_returns_403_when_the_user_cannot_access_the_library()
    {
        var response = await ClientFor("user-b-full").GetAsync($"/api/items/{Hobbit}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("user-no-explicit", Omens, HttpStatusCode.Forbidden)]
    [InlineData("user-no-explicit", Hobbit, HttpStatusCode.OK)]
    [InlineData("user-inactive", Omens, HttpStatusCode.Forbidden)]
    // user-admin has no accessExplicitContent and no accessAllTags in its permissions JSON; Node denies both.
    [InlineData("user-admin", Omens, HttpStatusCode.Forbidden)]
    [InlineData("user-admin", Hobbit, HttpStatusCode.Forbidden)]
    // user-tags may only see tag "scifi" (Dune and Alpha carry it).
    [InlineData("user-tags", Dune, HttpStatusCode.OK)]
    [InlineData("user-tags", Alpha, HttpStatusCode.OK)]
    [InlineData("user-tags", Hobbit, HttpStatusCode.Forbidden)]
    [InlineData("user-tags", Omens, HttpStatusCode.Forbidden)]
    // user-hide-scifi may see everything except tag "scifi". Untagged items stay visible.
    [InlineData("user-hide-scifi", Dune, HttpStatusCode.Forbidden)]
    [InlineData("user-hide-scifi", Hobbit, HttpStatusCode.OK)]
    [InlineData("user-hide-scifi", Omens, HttpStatusCode.OK)]
    public async Task GetItem_applies_explicit_and_tag_rules_like_Node(string userId, string itemId, HttpStatusCode expected)
    {
        var response = await ClientFor(userId).GetAsync($"/api/items/{itemId}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_with_expanded_returns_501_so_Node_can_answer()
    {
        var response = await ClientFor("user-full").GetAsync($"/api/items/{Hobbit}?expanded=1");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_ignores_include_when_not_expanded_like_Node()
    {
        var response = await ClientFor("user-full").GetAsync($"/api/items/{Hobbit}?include=progress,rssfeed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_for_a_podcast_item_returns_501()
    {
        var response = await ClientFor("user-full").GetAsync($"/api/items/{Podcast}");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_uses_the_permission_service_for_access_decisions()
    {
        using var custom = new AbsApiFactory
        {
            IncludeW2Fixtures = false,
            ConfigureTestServices = services =>
            {
                services.RemoveAll<IPermissionService>();
                services.AddSingleton<IPermissionService>(new FakePermissionService(
                    true, [], new ItemAccessRules(false, true, false, [])));
            },
        };

        var allowed = await ClientFor("user-full", custom).GetAsync($"/api/items/{Hobbit}");
        var explicitDenied = await ClientFor("user-full", custom).GetAsync($"/api/items/{Omens}");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, explicitDenied.StatusCode);
    }

    // GET /api/libraries/:id/items

    [Theory]
    [MemberData(nameof(ListScenarios))]
    public async Task GetItems_matches_the_Node_response_exactly(string scenario)
    {
        var recorded = NodeItems["lists"]![scenario]!;
        var query = string.Join('&', recorded["query"]!.AsObject().Select(x => $"{x.Key}={x.Value!.GetValue<string>()}"));
        if (scenario.EndsWith("ignore-prefix"))
        {
            factory.Execute("UPDATE settings SET value = '{\"id\":\"server-settings\",\"sortingIgnorePrefix\":true,\"sortingPrefixes\":[\"the\",\"a\",\"an\"]}' WHERE key = 'server-settings'");
        }

        var user = scenario == "no-explicit" ? "user-no-explicit" : "user-full";
        var response = await ClientFor(user).GetAsync(ListUrl(AbsApiFactory.LibraryA, $"?{query}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = await BodyOf(response);
        Assert.True(JsonNode.DeepEquals(recorded["response"], actual), $"Actual: {actual.ToJsonString()}");
    }

    [Fact]
    public async Task GetItems_keeps_the_envelope_property_order_of_Node()
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, "?sort=addedAt&minified=1"));

        var actual = (await BodyOf(response)).AsObject();
        var expected = NodeItems["lists"]!["minified-flag"]!["response"]!.AsObject();
        Assert.Equal(expected.Select(x => x.Key), actual.Select(x => x.Key));
        Assert.Equal(
            expected["results"]![0]!["media"]!.AsObject().Select(x => x.Key),
            actual["results"]![0]!["media"]!.AsObject().Select(x => x.Key));
    }

    [Fact]
    public async Task GetItems_without_limit_returns_every_item()
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, "?sort=addedAt"));

        var body = await BodyOf(response);
        Assert.Equal(6, body["total"]!.GetValue<int>());
        Assert.Equal(6, body["results"]!.AsArray().Count);
        Assert.Equal(0, body["limit"]!.GetValue<int>());
        Assert.Equal(0, body["offset"]!.GetValue<int>());
    }

    [Fact]
    public async Task GetItems_treats_a_non_numeric_limit_as_0_like_Node()
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, "?limit=abc"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await BodyOf(response))["limit"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("limit=-1", "Invalid request. limit must be a positive integer")]
    [InlineData("limit=1.5", "Invalid request. limit must be a positive integer")]
    [InlineData("page=-2", "Invalid request. page must be a positive integer")]
    public async Task GetItems_returns_400_for_bad_paging(string query, string message)
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, $"?{query}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetItems_returns_403_when_the_user_cannot_access_the_library()
    {
        var response = await ClientFor("user-b-full").GetAsync(ListUrl(AbsApiFactory.LibraryA));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetItems_returns_404_for_an_unknown_library()
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl("no-such-library"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Library not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetItems_returns_401_without_authentication()
    {
        var response = await ClientFor(null).GetAsync(ListUrl(AbsApiFactory.LibraryA));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("filter=genres.RmFudGFzeQ%3D%3D")]
    [InlineData("collapseseries=1")]
    [InlineData("include=rssfeed")]
    [InlineData("sort=progress")]
    [InlineData("sort=random")]
    [InlineData("sort=nonsense")]
    public async Task GetItems_returns_501_for_options_that_stay_with_Node(string query)
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, $"?{query}"));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_for_a_podcast_library_returns_501()
    {
        var response = await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryB));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_for_a_tag_restricted_user_returns_501()
    {
        var response = await ClientFor("user-tags").GetAsync(ListUrl(AbsApiFactory.LibraryA, "?sort=addedAt"));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_sequence_sort_falls_back_to_the_title_sort_like_Node()
    {
        var sequence = await BodyOf(await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, "?sort=sequence")));
        var title = await BodyOf(await ClientFor("user-full").GetAsync(ListUrl(AbsApiFactory.LibraryA, "?sort=media.metadata.title")));

        Assert.Equal(
            title["results"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()),
            sequence["results"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()));
    }
}
