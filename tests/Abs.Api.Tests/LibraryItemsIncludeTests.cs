using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Abs.Api.Tests;

/// <summary>
/// ?include= on GET /api/libraries/:id/items. Node: server/controllers/LibraryController.js:605-621 (the echoed list),
/// server/utils/queries/libraryItemsBookFilters.js:407-408, 670-677 and server/models/LibraryItem.js:308-321 (rssFeed, mediaItemShare).
/// The same requests are compared with a running Node in tests/e2e/run_e2e.py.
/// </summary>
public class LibraryItemsIncludeTests
{
    private const string Date = "2026-10-06 22:08:12.334 +00:00";

    private static string Token(string secret, string user)
    {
        static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var data = header + "." + Base64(JsonSerializer.SerializeToUtf8Bytes(new { userId = user, exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }));
        return data + "." + Base64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));
    }

    private static string Url(string query) => $"/api/libraries/{AbsApiFactory.LibraryA}/items?{query}";

    private static async Task<JsonNode> Get(AbsApiFactory factory, string secret, string user, string query)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(secret, user));
        var response = await client.GetAsync(Url(query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private static AbsApiFactory Factory(string secret) =>
        new() { EnvironmentName = "Production", TestJwtSecret = secret, IncludeW2Fixtures = false };

    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static void AddFeed(AbsApiFactory factory, string itemId) => factory.Execute(
        $@"INSERT INTO feeds (id, entityType, entityId, feedURL, title, description, ownerName, ownerEmail, preventIndexing, createdAt, updatedAt)
           VALUES ('feed-1', 'libraryItem', '{itemId}', 'http://example.test/feed/x', 'Feed title', 'Feed text', 'Owner', 'o@example.test', 1, '{Date}', '{Date}')");

    private static void AddShare(AbsApiFactory factory, string bookId) => factory.Execute(
        $"INSERT INTO mediaItemShares (id, mediaItemId, mediaItemType, slug, createdAt, updatedAt) VALUES ('share-1', '{bookId}', 'book', 'slug', '{Date}', '{Date}')");

    [Fact]
    public async Task Rssfeed_adds_the_feed_to_the_item_that_has_one_and_to_no_other()
    {
        var secret = Secret();
        using var factory = Factory(secret);
        var first = (await Get(factory, secret, "user-full", "sort=addedAt"))["results"]!.AsArray();
        var withFeed = first[0]!["id"]!.GetValue<string>();
        AddFeed(factory, withFeed);

        var body = await Get(factory, secret, "user-full", "sort=addedAt&include=rssfeed");

        var results = body["results"]!.AsArray();
        var feed = results.Single(x => x!["id"]!.GetValue<string>() == withFeed)!["rssFeed"]!;
        Assert.Equal("feed-1", feed["id"]!.GetValue<string>());
        Assert.Equal("libraryItem", feed["entityType"]!.GetValue<string>());
        Assert.Equal(withFeed, feed["entityId"]!.GetValue<string>());
        Assert.Equal("http://example.test/feed/x", feed["feedUrl"]!.GetValue<string>());
        Assert.Equal("Feed title", feed["meta"]!["title"]!.GetValue<string>());
        Assert.Equal("Feed text", feed["meta"]!["description"]!.GetValue<string>());
        Assert.True(feed["meta"]!["preventIndexing"]!.GetValue<bool>());
        Assert.Equal("Owner", feed["meta"]!["ownerName"]!.GetValue<string>());
        Assert.Equal("o@example.test", feed["meta"]!["ownerEmail"]!.GetValue<string>());
        Assert.All(results.Where(x => x!["id"]!.GetValue<string>() != withFeed), x => Assert.Null(x!["rssFeed"]));
    }

    [Fact]
    public async Task Without_rssfeed_in_include_the_feed_is_not_shown()
    {
        var secret = Secret();
        using var factory = Factory(secret);
        var item = (await Get(factory, secret, "user-full", "sort=addedAt"))["results"]![0]!["id"]!.GetValue<string>();
        AddFeed(factory, item);

        var body = await Get(factory, secret, "user-full", "sort=addedAt&include=numEpisodesIncomplete");

        Assert.All(body["results"]!.AsArray(), x => Assert.Null(x!["rssFeed"]));
    }

    // Node trims and lower-cases each value, drops empty ones and echoes the list; unknown values are ignored.
    [Fact]
    public async Task The_include_list_is_normalised_like_Node_does()
    {
        var secret = Secret();
        using var factory = Factory(secret);

        var body = await Get(factory, secret, "user-full", "include=%20RSSFeed%20,,foo,%20");

        Assert.Equal("rssfeed,foo", body["include"]!.GetValue<string>());
    }

    [Fact]
    public async Task Share_for_an_admin_answers_501_when_a_share_exists_for_a_listed_book()
    {
        var secret = Secret();
        using var factory = Factory(secret);
        var item = (await Get(factory, secret, "user-admin", "sort=addedAt"))["results"]![0]!;
        AddShare(factory, item["media"]!["id"]!.GetValue<string>());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(secret, "user-admin"));

        var response = await client.GetAsync(Url("sort=addedAt&include=share"));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Share_is_ignored_for_users_who_are_not_admins()
    {
        var secret = Secret();
        using var factory = Factory(secret);
        var item = (await Get(factory, secret, "user-full", "sort=addedAt"))["results"]![0]!;
        AddShare(factory, item["media"]!["id"]!.GetValue<string>());

        var body = await Get(factory, secret, "user-full", "sort=addedAt&include=share");

        Assert.All(body["results"]!.AsArray(), x => Assert.Null(x!["mediaItemShare"]));
    }

    [Fact]
    public async Task Share_for_an_admin_is_answered_when_no_share_exists()
    {
        var secret = Secret();
        using var factory = Factory(secret);

        var body = await Get(factory, secret, "user-admin", "sort=addedAt&include=share");

        Assert.NotEmpty(body["results"]!.AsArray());
        Assert.All(body["results"]!.AsArray(), x => Assert.Null(x!["mediaItemShare"]));
    }
}
