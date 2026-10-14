using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Abs.Api.Tests;

public class DIntegrationJwtTests
{
    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static string Token(string secret, string? user, long expiry)
    {
        static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        // Same userId claim as Node TokenManager. Key is generated per test, never a real credential.
        var payload = Base64(JsonSerializer.SerializeToUtf8Bytes(new { userId = user, exp = expiry }));
        var data = header + "." + payload;
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data));
        return data + "." + Base64(signature);
    }

    [Theory]
    [InlineData("/api/me/progress")]
    [InlineData("/api/me/bookmarks")]
    [InlineData("/api/me")]
    public async Task Production_accepts_signed_token_and_reads_Sequelize_user_dates(string route)
    {
        var secret = Secret();
        using var factory = new AbsApiFactory { EnvironmentName = "Production", TestJwtSecret = secret };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(secret, "w2-bm-alice",
            DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()));
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (route == "/api/me")
        {
            var user = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.Equal("w2-bm-alice", user["id"]!.GetValue<string>());
            Assert.Equal(1790812800000, user["createdAt"]!.GetValue<long>());
        }
        if (route == "/api/me/bookmarks")
        {
            var bookmarks = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["bookmarks"]!.AsArray();
            Assert.Equal(4, bookmarks.Count);
            Assert.DoesNotContain(bookmarks, x => x!["title"]?.ToString() == "bob-only");
        }
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-signature")]
    [InlineData("missing-user")]
    [InlineData("inactive")]
    [InlineData("unknown")]
    public async Task Invalid_or_ineligible_tokens_are_unauthorized(string kind)
    {
        var secret = Secret();
        using var factory = new AbsApiFactory { EnvironmentName = "Production", TestJwtSecret = secret };
        using var client = factory.CreateClient();
        var user = kind switch
        {
            "missing-user" => null,
            "inactive" => "w2-bm-inactive",
            "unknown" => "not-a-user",
            _ => "w2-bm-alice"
        };
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(kind == "wrong-signature" ? Secret() : secret,
            user, DateTimeOffset.UtcNow.AddMinutes(kind == "expired" ? -5 : 5).ToUnixTimeSeconds()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/bookmarks")).StatusCode);
    }
}
