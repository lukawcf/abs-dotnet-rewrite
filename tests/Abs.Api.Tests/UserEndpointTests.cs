using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Abs.Api.Tests;

/// <summary>
/// GET /api/me and GET /api/users against the shape of Node's User.toOldJSONForBrowser (server/models/User.js:603-639).
/// The same requests are compared with a running Node in tests/e2e/run_e2e.py.
/// </summary>
public class UserEndpointTests
{
    private const string Date = "2026-10-06 22:08:12.334 +00:00";

    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    // Key is generated per test, never a real credential. Without an exp claim the token is an "old token" in Node's terms.
    private static string Token(string secret, string user, bool withExpiry)
    {
        static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Base64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        object claims = withExpiry
            ? new { userId = user, exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() }
            : new { userId = user };
        var data = header + "." + Base64(JsonSerializer.SerializeToUtf8Bytes(claims));
        return data + "." + Base64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));
    }

    private static AbsApiFactory FactoryWithUsers(string secret)
    {
        var factory = new AbsApiFactory { EnvironmentName = "Production", TestJwtSecret = secret };
        factory.Execute($@"INSERT INTO users (id, username, email, type, token, isActive, isLocked, lastSeen, permissions, extraData, createdAt, updatedAt) VALUES
            ('u-root', 'root', NULL, 'root', 'root-legacy-token', 1, 0, NULL, '{{""accessAllLibraries"":true,""librariesAccessible"":[]}}', NULL, '{Date}', '{Date}'),
            ('u-full', 'full', 'full@example.test', 'user', 'full-legacy-token', 1, 0, '{Date}', '{{""accessAllLibraries"":false,""librariesAccessible"":[""l1""],""itemTagsSelected"":[""t1""]}}', '{{""seriesHideFromContinueListening"":[""s1""]}}', '{Date}', '{Date}')");
        return factory;
    }

    private static HttpClient Client(AbsApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    // Regression: a 30 character JWT_SECRET_KEY (240 bits) is valid for Node but was rejected by .NET (IDX10517).
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(32)]
    [InlineData(64)]
    public async Task Tokens_signed_with_a_secret_of_any_length_are_accepted(int length)
    {
        var secret = new string('k', length - 1) + "Z";
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, "u-full", withExpiry: true)).GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_stored_user_fields()
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, "u-full", withExpiry: true)).GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("full-legacy-token", user["token"]!.GetValue<string>());
        Assert.Equal("full@example.test", user["email"]!.GetValue<string>());
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T22:08:12.334Z").ToUnixTimeMilliseconds(), user["lastSeen"]!.GetValue<long>());
        Assert.Equal("s1", user["seriesHideFromContinueListening"]![0]!.GetValue<string>());
        Assert.Equal("l1", user["librariesAccessible"]![0]!.GetValue<string>());
        Assert.Equal("t1", user["itemTagsSelected"]![0]!.GetValue<string>());
        Assert.Null(user["permissions"]!["librariesAccessible"]);
        Assert.NotNull(user["mediaProgress"]);
        Assert.NotNull(user["bookmarks"]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Me_reports_isOldToken_only_for_tokens_without_expiry(bool withExpiry, bool expectedPresent)
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, "u-full", withExpiry)).GetAsync("/api/me");

        var user = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(expectedPresent, user.ContainsKey("isOldToken"));
        if (expectedPresent)
            Assert.True(user["isOldToken"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Users_list_is_minimal_and_hides_the_root_token_from_an_admin()
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        // user-admin (type admin) comes from seed.sql
        var response = await Client(factory, Token(secret, "user-admin", withExpiry: true)).GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["users"]!.AsArray();
        Assert.All(users, u =>
        {
            Assert.False(u!.AsObject().ContainsKey("mediaProgress"));
            Assert.False(u.AsObject().ContainsKey("bookmarks"));
        });
        Assert.Equal(string.Empty, users.Single(u => u!["id"]!.GetValue<string>() == "u-root")!["token"]!.GetValue<string>());
        Assert.Equal("full-legacy-token", users.Single(u => u!["id"]!.GetValue<string>() == "u-full")!["token"]!.GetValue<string>());
    }

    [Fact]
    public async Task Users_list_shows_the_root_token_to_root()
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, "u-root", withExpiry: true)).GetAsync("/api/users");

        var users = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["users"]!.AsArray();
        Assert.Equal("root-legacy-token", users.Single(u => u!["id"]!.GetValue<string>() == "u-root")!["token"]!.GetValue<string>());
    }

    [Fact]
    public async Task Users_list_is_in_creation_order()
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, "u-root", withExpiry: true)).GetAsync("/api/users");

        var created = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["users"]!.AsArray()
            .Select(u => u!["createdAt"]!.GetValue<long>()).ToList();
        Assert.Equal(created.Order().ToList(), created);
    }

    [Theory]
    [InlineData("user-only-a", HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData("user-admin", HttpStatusCode.NotImplemented, "Not Implemented")]
    public async Task User_by_id_stays_in_Node(string caller, HttpStatusCode status, string body)
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, caller, withExpiry: true)).GetAsync("/api/users/u-full");

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Users_list_with_include_latestSession_stays_in_Node()
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var response = await Client(factory, Token(secret, "u-root", withExpiry: true)).GetAsync("/api/users?include=latestSession");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    // Node answers with res.sendStatus: the body is the plain reason phrase, not a ProblemDetails document.
    [Fact]
    public async Task Error_statuses_have_plain_text_bodies_like_Node()
    {
        var secret = Secret();
        using var factory = FactoryWithUsers(secret);

        var anonymous = await factory.CreateClient().GetAsync("/api/libraries");
        var forbidden = await Client(factory, Token(secret, "user-only-a", withExpiry: true)).GetAsync("/api/users");
        var missing = await Client(factory, Token(secret, "u-root", withExpiry: true)).GetAsync("/api/items/nope");

        Assert.Equal((HttpStatusCode.Unauthorized, "Unauthorized"), (anonymous.StatusCode, await anonymous.Content.ReadAsStringAsync()));
        Assert.Equal((HttpStatusCode.Forbidden, "Forbidden"), (forbidden.StatusCode, await forbidden.Content.ReadAsStringAsync()));
        Assert.Equal((HttpStatusCode.NotFound, "Not Found"), (missing.StatusCode, await missing.Content.ReadAsStringAsync()));
    }
}
