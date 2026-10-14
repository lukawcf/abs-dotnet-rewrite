using System.Net;
using System.Text.Json.Nodes;
using Abs.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.Api.Tests;

public class W2BookmarkReadTests
{
    private static JsonObject Input => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "w2-bookmark-input.json")))!.AsObject();
    private static JsonArray Expected => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "w2-node-bookmark-reads.json")))!.AsArray();
    public static IEnumerable<object[]> Cases => Input["cases"]!.AsArray()
        .Select(x => new object[] { x!["name"]!.GetValue<string>() });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_real_Node_bookmark_and_permission_methods(string name)
    {
        var input = Input["cases"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == name)!;
        var expected = Expected.Single(x => x!["name"]!.GetValue<string>() == name)!;
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", input["user"]!.GetValue<string>());
        var route = "/api/me/bookmarks" + (input["item"] is null ? "" : "/" + input["item"]!.GetValue<string>());
        var response = await client.GetAsync(route);
        Assert.Equal(expected["status"]!.GetValue<int>(), (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Assert.Equal(expected["body"]!.GetValue<string>(), body);
            return;
        }
        var actual = JsonNode.Parse(body);
        Assert.True(JsonNode.DeepEquals(expected["body"], actual), body);
        var expectedRows = expected["body"]!["bookmarks"]!.AsArray();
        var actualRows = actual!["bookmarks"]!.AsArray();
        for (var i = 0; i < expectedRows.Count; i++)
            Assert.Equal(expectedRows[i]!.AsObject().Select(x => x.Key), actualRows[i]!.AsObject().Select(x => x.Key));
    }

    [Theory]
    [InlineData("/api/me/bookmarks")]
    [InlineData("/api/me/bookmarks/w2-item")]
    public async Task Anonymous_read_is_unauthorized(string route)
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Reading_bookmarks_does_not_change_stored_JSON()
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-bm-alice");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
        var before = await db.Users.OrderBy(x => x.Id).Select(x => EF.Property<string?>(x, "DBookmarks")).ToListAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/bookmarks")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/bookmarks/w2-item")).StatusCode);
        var after = await db.Users.OrderBy(x => x.Id).Select(x => EF.Property<string?>(x, "DBookmarks")).ToListAsync();
        Assert.Equal(before, after);
    }
}
