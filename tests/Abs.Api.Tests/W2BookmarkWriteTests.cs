using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abs.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.Api.Tests;

public class W2BookmarkWriteTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Failed_bookmark_save_rolls_back_and_preserves_user_fields(string method)
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-bm-alice");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
        async Task<string> Snapshot() => JsonSerializer.Serialize(await db.Users.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.Type,
                x.IsActive,
                x.Permissions,
                Bookmarks = EF.Property<string?>(x, "DBookmarks"),
                UpdatedAt = EF.Property<DateTimeOffset>(x, "DBookmarkUpdatedAt")
            }).ToListAsync());
        async Task<string> UserFields() => JsonSerializer.Serialize(await db.Users.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Username, x.Type, x.IsActive, x.Permissions }).ToListAsync());
        var before = await Snapshot();
        var fields = await UserFields();
        // Failure injection is confined to the synthetic SQLite test database.
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_bookmark_save BEFORE UPDATE ON users WHEN NEW.id = 'w2-bm-alice' BEGIN SELECT RAISE(ABORT, 'injected bookmark failure'); END;");
        async Task<HttpResponseMessage> Write() => method switch
        {
            "POST" => await client.PostAsJsonAsync("/api/me/item/w2-item/bookmark", new { time = 36, title = "new" }),
            "PATCH" => await client.PatchAsJsonAsync("/api/me/item/w2-item/bookmark", new { time = 12, title = "changed" }),
            _ => await client.DeleteAsync("/api/me/item/w2-item/bookmark/12")
        };
        Assert.Equal(HttpStatusCode.InternalServerError, (await Write()).StatusCode);
        Assert.Equal(before, await Snapshot());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_bookmark_save;");
        Assert.Equal(HttpStatusCode.OK, (await Write()).StatusCode);
        Assert.Equal(fields, await UserFields());
    }

    [Fact]
    public async Task Large_unsigned_hex_time_does_not_match_negative_time()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-bm-alice");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/me/item/w2-item/bookmark",
            new { time = -1, title = "negative" })).StatusCode);
        var created = await client.PostAsJsonAsync("/api/me/item/w2-item/bookmark",
            new { time = "0xFFFFFFFFFFFFFFFF", title = "unsigned" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var bookmark = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        Assert.Equal("0xFFFFFFFFFFFFFFFF", bookmark["time"]!.GetValue<string>());
    }

    private static JsonArray Load(string name) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name)))!.AsArray();
    public static IEnumerable<object[]> Cases => Load("w2-bookmark-write-cases.json")
        .Select(x => new object[] { x!["name"]!.GetValue<string>() });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_Node_response_and_all_users_persisted_bookmarks(string name)
    {
        var input = Load("w2-bookmark-write-cases.json").Single(x => x!["name"]!.GetValue<string>() == name)!;
        var expected = Load("w2-node-bookmark-writes.json").Single(x => x!["name"]!.GetValue<string>() == name)!;
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", input["user"]?.GetValue<string>() ?? "w2-bm-alice");
        var route = "/api/me/item/" + (input["item"]?.GetValue<string>() ?? "w2-item") + "/bookmark";
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var response = input["method"]!.GetValue<string>() switch
        {
            "POST" => await client.PostAsJsonAsync(route, input["payload"]),
            "PATCH" => await client.PatchAsJsonAsync(route, input["payload"]),
            _ => await client.DeleteAsync(route + "/" + input["time"]!.GetValue<string>())
        };
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.Equal(expected["status"]!.GetValue<int>(), (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        if (expected["body"] is JsonValue text && text.TryGetValue<string>(out var message))
            Assert.Equal(message, body);
        else
        {
            var actual = JsonNode.Parse(body)!;
            Normalize(expected["body"]!, actual, before, after);
            Assert.True(JsonNode.DeepEquals(expected["body"], actual), body);
            Assert.Equal(expected["body"]!.AsObject().Select(x => x.Key), actual.AsObject().Select(x => x.Key));
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
        var users = await db.Users.Where(x => x.Id.StartsWith("w2-bm-")).OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Bookmarks = EF.Property<string?>(x, "DBookmarks"),
                UpdatedAt = EF.Property<DateTimeOffset>(x, "DBookmarkUpdatedAt")
            }).ToListAsync();
        var actualUsers = new JsonArray(users.Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id,
            ["bookmarks"] = x.Bookmarks is null ? null : JsonNode.Parse(x.Bookmarks),
            ["updatedAt"] = x.UpdatedAt.ToUnixTimeMilliseconds()
        }).ToArray());
        Normalize(expected["users"]!, actualUsers, before, after);
        Assert.True(JsonNode.DeepEquals(expected["users"], actualUsers), $"Expected: {expected["users"]}; Actual: {actualUsers}");
    }

    private static void Normalize(JsonNode expected, JsonNode actual, long before, long after)
    {
        if (expected is JsonObject obj)
            foreach (var (key, value) in obj)
            {
                if (value is JsonValue marker && marker.TryGetValue<string>(out var text) && text == "$generated-time")
                {
                    Assert.InRange(actual[key]!.GetValue<long>(), before, after);
                    actual[key] = text;
                }
                else if (value is JsonObject or JsonArray) Normalize(value, actual[key]!, before, after);
            }
        else if (expected is JsonArray array)
        {
            Assert.Equal(array.Count, actual.AsArray().Count);
            for (var i = 0; i < array.Count; i++)
                if (array[i] is JsonObject or JsonArray) Normalize(array[i]!, actual[i]!, before, after);
        }
    }

    [Theory]
    [InlineData("POST", false, 501)]
    [InlineData("PATCH", false, 501)]
    [InlineData("DELETE", false, 501)]
    [InlineData("POST", true, 401)]
    [InlineData("PATCH", true, 401)]
    [InlineData("DELETE", true, 401)]
    public async Task Default_off_or_anonymous_write_leaves_all_users_unchanged(string method, bool anonymous, int status)
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = anonymous };
        using var client = factory.CreateClient();
        if (!anonymous) client.DefaultRequestHeaders.Add("X-Dev-User", "w2-bm-alice");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
        async Task<string> Snapshot() => JsonSerializer.Serialize(await db.Users.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Bookmarks = EF.Property<string?>(x, "DBookmarks"),
                UpdatedAt = EF.Property<DateTimeOffset>(x, "DBookmarkUpdatedAt"),
                x.Permissions
            }).ToListAsync());
        var before = await Snapshot();
        var route = "/api/me/item/w2-item/bookmark";
        var response = method switch
        {
            "POST" => await client.PostAsJsonAsync(route, new { time = 12, title = "changed" }),
            "PATCH" => await client.PatchAsJsonAsync(route, new { time = 12, title = "changed" }),
            _ => await client.DeleteAsync(route + "/12")
        };
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(before, await Snapshot());
    }
}
