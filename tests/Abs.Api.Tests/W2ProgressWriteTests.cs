using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.Api.Tests;

public class W2ProgressWriteTests
{
    [Theory]
    [InlineData("{\"currentTime\":[]}")]
    [InlineData("{\"currentTime\":\"invalid\"}")]
    [InlineData("{\"currentTime\":1e400}")]
    [InlineData("{\"progress\":-1e400}")]
    [InlineData("{\"isFinished\":\"maybe\"}")]
    [InlineData("{\"finishedAt\":\"invalid\"}")]
    public async Task Unsupported_values_do_not_mutate_data(string payload)
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        var before = await client.GetStringAsync("/api/me/progress/w2-item");
        var response = await client.PatchAsJsonAsync("/api/me/progress/w2-item", JsonNode.Parse(payload));
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/me/progress/w2-item"));
    }

    [Fact]
    public async Task Partial_update_preserves_unknown_extra_data()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AbsDbContext>();
            var row = await db.Set<PlaybackProgress>().AsTracking().SingleAsync(x => x.Id == "w2-book-progress");
            var extra = JsonNode.Parse(row.ExtraData!)!.AsObject();
            extra["extension"] = "preserved";
            row.ExtraData = extra.ToJsonString();
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/me/progress/w2-item",
            new { currentTime = 80 })).StatusCode);
        using var readScope = factory.Services.CreateScope();
        var persisted = await readScope.ServiceProvider.GetRequiredService<AbsDbContext>()
            .Set<PlaybackProgress>().SingleAsync(x => x.Id == "w2-book-progress");
        Assert.Equal("preserved", JsonNode.Parse(persisted.ExtraData!)!["extension"]!.GetValue<string>());
    }

    private static JsonArray Load(string file) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", file)))!.AsArray();

    public static IEnumerable<object[]> Cases => Load("w2-write-cases.json")
        .Select(x => new object[] { x!["name"]!.GetValue<string>() });

    public static IEnumerable<object[]> CoercionCases => Load("w2-coercion-cases.json")
        .Select(x => new object[] { x!["name"]!.GetValue<string>() });

    [Theory]
    [MemberData(nameof(CoercionCases))]
    public Task Scalar_flag_coercion_matches_Node(string name) =>
        CheckPatch(name, "w2-coercion-cases.json", "w2-node-coercion.json");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task Single_patch_matches_Node_persisted_result(string name) =>
        CheckPatch(name, "w2-write-cases.json", "w2-node-progress-writes.json");

    private static async Task CheckPatch(string name, string casesFile, string expectedFile)
    {
        var input = Load(casesFile).Single(x => x!["name"]!.GetValue<string>() == name)!;
        var expected = Load(expectedFile).Single(x => x!["name"]!.GetValue<string>() == name)!;
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", input["user"]?.GetValue<string>() ?? "w2-alice");
        var route = "/api/me/progress/" + input["item"]!.GetValue<string>() +
            (input["episode"] is null ? "" : "/" + input["episode"]!.GetValue<string>());
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var response = await client.PatchAsJsonAsync(route, input["patch"]);
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.Equal(expected["status"]!.GetValue<int>(), (int)response.StatusCode);
        Assert.Equal(expected["message"]!.GetValue<string>(), await response.Content.ReadAsStringAsync());
        if (expected["row"] is not { } row) return;
        var read = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var actual = JsonNode.Parse(await read.Content.ReadAsStringAsync())!.AsObject();
        if (row["id"]!.GetValue<string>() == "$generated-id")
        {
            Assert.True(Guid.TryParse(actual["id"]!.GetValue<string>(), out _));
            actual["id"] = "$generated-id";
        }
        foreach (var field in new[] { "lastUpdate", "startedAt", "finishedAt" })
        {
            if (row[field] is JsonValue v && v.TryGetValue<string>(out var marker) && marker == "$generated-time")
            {
                Assert.InRange(actual[field]!.GetValue<long>(), before, after);
                actual[field] = marker;
            }
        }
        Assert.True(JsonNode.DeepEquals(row, actual), $"Expected: {row}; Actual: {actual}");
    }

    [Fact]
    public async Task Writes_default_off_and_leave_row_unchanged()
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        var before = await client.GetStringAsync("/api/me/progress/w2-item");
        var response = await client.PatchAsJsonAsync("/api/me/progress/w2-item", new { currentTime = 199 });
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/api/me/progress/w2-item"));
    }

    [Fact]
    public async Task Anonymous_write_is_unauthorized()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PatchAsJsonAsync("/api/me/progress/w2-item", new { currentTime = 80 })).StatusCode);
    }

    [Fact]
    public async Task Creation_for_other_user_does_not_change_existing_owner()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var bob = factory.CreateClient();
        using var alice = factory.CreateClient();
        bob.DefaultRequestHeaders.Add("X-Dev-User", "w2-bob");
        alice.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        var before = await bob.GetStringAsync("/api/me/progress/w2-private-item");
        Assert.Equal(HttpStatusCode.OK,
            (await alice.PatchAsJsonAsync("/api/me/progress/w2-private-item", new { currentTime = 10 })).StatusCode);
        Assert.Equal(before, await bob.GetStringAsync("/api/me/progress/w2-private-item"));
    }

    [Fact]
    public async Task Batch_without_array_payload_is_rejected()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync("/api/me/progress/batch/update", new { currentTime = 99 })).StatusCode);
    }

    [Fact]
    public async Task Identity_fields_cannot_transfer_progress_to_another_user()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/me/progress/w2-item",
            new { userId = "w2-bob", id = "stolen", mediaItemId = "w2-bob-book", currentTime = 80 })).StatusCode);
        var row = JsonNode.Parse(await client.GetStringAsync("/api/me/progress/w2-item"))!;
        Assert.Equal("w2-alice", row["userId"]!.GetValue<string>());
        Assert.Equal("w2-book-progress", row["id"]!.GetValue<string>());
        Assert.Equal("w2-book", row["mediaItemId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Non_object_payload_rejected()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync("/api/me/progress/w2-item", new[] { 1 })).StatusCode);
    }
}
