using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Abs.Api.Dtos;
using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.Api.Tests;

public class W2BatchDeleteTests
{
    [Fact]
    public async Task Database_failure_stops_batch_but_preserves_earlier_success()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
            // Synthetic failure injection in the test DB only, never application SQL.
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_new_progress BEFORE INSERT ON mediaProgresses WHEN NEW.mediaItemId = 'w2-new-book' BEGIN SELECT RAISE(ABORT, 'injected failure'); END;");
        }
        var payload = JsonNode.Parse("[{\"libraryItemId\":\"w2-item\",\"currentTime\":80},{\"libraryItemId\":\"w2-new-item\",\"currentTime\":30},{\"libraryItemId\":\"w2-item\",\"currentTime\":90}]");
        // Development exception middleware exposes the unhandled database failure as HTTP 500.
        var failed = await client.PatchAsJsonAsync("/api/me/progress/batch/update", payload);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var row = JsonNode.Parse(await client.GetStringAsync("/api/me/progress/w2-item"))!;
        Assert.Equal(80, row["currentTime"]!.GetValue<double>());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/progress/w2-new-item")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/me/progress/batch/update",
            new[] { new { libraryItemId = "w2-item", currentTime = 85 } })).StatusCode);
    }

    [Fact]
    public async Task Missing_batch_body_matches_Node_plain_error()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/me/progress/batch/update");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Missing request payload", await response.Content.ReadAsStringAsync());
    }

    private static JsonArray Load(string file) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", file)))!.AsArray();
    public static IEnumerable<object[]> Cases => Load("w2-batch-delete-cases.json")
        .Select(x => new object[] { x!["name"]!.GetValue<string>() });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_Node_status_and_all_persisted_users(string name)
    {
        var input = Load("w2-batch-delete-cases.json").Single(x => x!["name"]!.GetValue<string>() == name)!;
        var expected = Load("w2-node-batch-delete.json").Single(x => x!["name"]!.GetValue<string>() == name)!;
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var repeat = input["repeat"]?.GetValue<int>() ?? 1;
        for (var i = 0; i < repeat; i++)
        {
            var response = input["method"]!.GetValue<string>() == "DELETE"
                ? await client.DeleteAsync("/api/me/progress/" + input["id"]!.GetValue<string>())
                : await client.PatchAsJsonAsync("/api/me/progress/batch/update", input["payload"]);
            Assert.Equal(expected["statuses"]![i]!.GetValue<int>(), (int)response.StatusCode);
            Assert.Equal(expected["messages"]![i]!.GetValue<string>(), await response.Content.ReadAsStringAsync());
        }
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
        var rows = (await db.Set<PlaybackProgress>().ToListAsync()).Select(PlaybackProgressDto.From)
            .OrderBy(x => x.UserId + "/" + x.MediaItemId, StringComparer.Ordinal).ToArray();
        var actual = System.Text.Json.JsonSerializer.SerializeToNode(rows,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.AsArray();
        var expectedRows = expected["rows"]!.AsArray();
        Assert.Equal(expectedRows.Count, actual.Count);
        for (var i = 0; i < actual.Count; i++)
        {
            if (expectedRows[i]!["id"]!.GetValue<string>() == "$generated-id")
            {
                Assert.True(Guid.TryParse(actual[i]!["id"]!.GetValue<string>(), out _));
                actual[i]!["id"] = "$generated-id";
            }
            foreach (var field in new[] { "lastUpdate", "startedAt", "finishedAt" })
            {
                if (expectedRows[i]![field] is JsonValue v && v.TryGetValue<string>(out var marker) && marker == "$generated-time")
                {
                    Assert.InRange(actual[i]![field]!.GetValue<long>(), before, after);
                    actual[i]![field] = marker;
                }
            }
        }
        Assert.True(JsonNode.DeepEquals(expectedRows, actual), $"Expected: {expectedRows}; Actual: {actual}");
    }

    [Theory]
    [InlineData(false, true, 501)]
    [InlineData(false, false, 501)]
    [InlineData(true, true, 401)]
    [InlineData(true, false, 401)]
    public async Task Disabled_or_anonymous_requests_do_not_mutate(bool anonymous, bool batch, int status)
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = anonymous };
        using var client = factory.CreateClient();
        if (!anonymous) client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
        var before = await db.Set<PlaybackProgress>().OrderBy(x => x.Id).ToListAsync();
        var snapshot = System.Text.Json.JsonSerializer.Serialize(before);
        var response = batch ? await client.PatchAsJsonAsync("/api/me/progress/batch/update",
            new[] { new { libraryItemId = "w2-item", currentTime = 99 } })
            : await client.DeleteAsync("/api/me/progress/w2-book-progress");
        Assert.Equal(status, (int)response.StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal(snapshot, System.Text.Json.JsonSerializer.Serialize(
            await db.Set<PlaybackProgress>().OrderBy(x => x.Id).ToListAsync()));
    }

    [Fact]
    public async Task Unsupported_entries_do_not_block_later_valid_updates()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        var payload = JsonNode.Parse("[null,{},7,{\"libraryItemId\":\"w2-item\",\"currentTime\":[]},{\"libraryItemId\":\"w2-item\",\"currentTime\":85}]");
        var response = await client.PatchAsJsonAsync("/api/me/progress/batch/update", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = JsonNode.Parse(await client.GetStringAsync("/api/me/progress/w2-item"))!;
        Assert.Equal(85, row["currentTime"]!.GetValue<double>());
    }
}
