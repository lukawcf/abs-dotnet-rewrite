using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace Abs.Api.Tests;

public class W2ProgressReadTests
{
    private static JsonObject Expected => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "w2-node-progress-reads.json")))!.AsObject();

    [Theory]
    [InlineData("/api/me/progress", "all")]
    [InlineData("/api/me/progress/w2-item", "book")]
    [InlineData("/api/me/progress/w2-podcast/w2-episode", "episode")]
    [InlineData("/api/me/progress/w2-book", "missing")]
    [InlineData("/api/me/progress/w2-private-item", "otherUser")]
    public async Task Matches_real_Node_methods(string route, string fixture)
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        var response = await client.GetAsync(route);
        var expected = Expected[fixture]!;
        Assert.Equal(expected["status"]!.GetValue<int>(), (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Assert.Equal(expected["body"]!.GetValue<string>(), body);
            return;
        }

        var actual = JsonNode.Parse(body);
        Assert.True(JsonNode.DeepEquals(expected["body"], actual), body);
        var expectedRow = fixture == "all" ? expected["body"]!["mediaProgress"]![0] : expected["body"];
        var actualRow = fixture == "all" ? actual!["mediaProgress"]![0] : actual;
        Assert.Equal(expectedRow!.AsObject().Select(x => x.Key), actualRow!.AsObject().Select(x => x.Key));
    }

    [Theory]
    [InlineData("/api/me/progress")]
    [InlineData("/api/me/progress/w2-item")]
    [InlineData("/api/me/progress/w2-podcast/w2-episode")]
    public async Task Anonymous_requests_are_rejected(string route)
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task User_without_progress_gets_empty_list()
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-empty");
        var response = await client.GetAsync("/api/me/progress");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"mediaProgress\":[]}"),
            JsonNode.Parse(await response.Content.ReadAsStringAsync())));
    }

    [Theory]
    [InlineData("/api/me/progress/w2-podcast/wrong-episode", 404)]
    [InlineData("/api/me/progress/w2-item/remove-from-continue-listening", 501)]
    public async Task Wrong_episode_and_unsupported_mutating_action(string route, int status)
    {
        using var factory = new AbsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        Assert.Equal(status, (int)(await client.GetAsync(route)).StatusCode);
    }
}
