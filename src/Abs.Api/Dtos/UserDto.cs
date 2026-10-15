using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

/// <summary>
/// JSON shape of Node's user.toOldJSONForBrowser(hideRootToken, minimal) (server/models/User.js:603-639).
/// MediaProgress, Bookmarks and IsOldToken are omitted when null, as Node omits undefined and the minimal form.
/// </summary>
public record UserDto(
    string Id,
    string? Username,
    string? Email,
    string? Type,
    string? Token,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsOldToken,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PlaybackProgressDto>? MediaProgress,
    IReadOnlyList<JsonElement> SeriesHideFromContinueListening,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<JsonElement>? Bookmarks,
    bool? IsActive,
    bool? IsLocked,
    long? LastSeen,
    long CreatedAt,
    JsonObject Permissions,
    IReadOnlyList<string> LibrariesAccessible,
    IReadOnlyList<string> ItemTagsSelected,
    bool HasOpenIDLink)
{
    /// <param name="hideRootToken">Node hides the root user's token from non-root callers.</param>
    /// <param name="minimal">Leaves out mediaProgress and bookmarks (the user list).</param>
    public static UserDto From(
        User user,
        bool hideRootToken,
        bool minimal,
        bool isOldToken,
        IReadOnlyList<PlaybackProgressDto> mediaProgress,
        IReadOnlyList<JsonElement> bookmarks)
    {
        var (permissions, librariesAccessible, itemTagsSelected) = ParsePermissions(user.Permissions);

        return new UserDto(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            Type: user.Type,
            Token: user.Type == "root" && hideRootToken ? string.Empty : user.Token,
            IsOldToken: isOldToken ? true : null,
            MediaProgress: minimal ? null : mediaProgress,
            SeriesHideFromContinueListening: ReadSeriesHidden(user.ExtraData),
            Bookmarks: minimal ? null : bookmarks,
            IsActive: user.IsActive,
            IsLocked: user.IsLocked,
            LastSeen: user.LastSeen?.ToUnixTimeMilliseconds(),
            CreatedAt: user.CreatedAt.ToUnixTimeMilliseconds(),
            Permissions: permissions,
            LibrariesAccessible: librariesAccessible,
            ItemTagsSelected: itemTagsSelected,
            HasOpenIDLink: false); // users.authOpenIDSub does not exist in the 2.37.1 schema read so far
    }

    private static List<JsonElement> ReadSeriesHidden(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(extraData);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("seriesHideFromContinueListening", out var list) &&
                   list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(x => x.Clone()).ToList()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static (JsonObject permissions, List<string> librariesAccessible, List<string> itemTagsSelected)
        ParsePermissions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (new JsonObject(), [], []);

        try
        {
            var root = JsonDocument.Parse(json).RootElement;

            // Node spreads these two lists at the top level and removes them from permissions.
            var librariesAccessible = root.TryGetProperty("librariesAccessible", out var la) && la.ValueKind == JsonValueKind.Array
                ? la.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                : new List<string>();

            var itemTagsSelected = root.TryGetProperty("itemTagsSelected", out var its) && its.ValueKind == JsonValueKind.Array
                ? its.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                : new List<string>();

            var permObj = JsonNode.Parse(json)!.AsObject();
            permObj.Remove("librariesAccessible");
            permObj.Remove("itemTagsSelected");

            return (permObj, librariesAccessible, itemTagsSelected);
        }
        catch
        {
            return (new JsonObject(), [], []);
        }
    }
}
