using System.Text.Json;
using System.Text.Json.Nodes;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

/// <summary>
/// JSON shape of Node's user.toOldJSONForBrowser().
/// Known limitations:
///   - token: empty string (not stored in users table)
///   - mediaProgress / bookmarks: empty arrays (owned by Person D)
///   - createdAt: from DB createdAt field
/// </summary>
public record UserDto(
    string Id,
    string? Username,
    string? Email,
    string? Type,
    string Token,
    bool IsOldToken,
    IReadOnlyList<object> MediaProgress,
    IReadOnlyList<string> SeriesHideFromContinueListening,
    IReadOnlyList<object> Bookmarks,
    bool IsActive,
    bool IsLocked,
    long? LastSeen,
    long CreatedAt,
    JsonObject Permissions,
    IReadOnlyList<string> LibrariesAccessible,
    IReadOnlyList<string> ItemTagsSelected,
    bool HasOpenIDLink)
{
    public static UserDto From(User user)
    {
        var (permissions, librariesAccessible, itemTagsSelected) = ParsePermissions(user.Permissions);

        return new UserDto(
            Id: user.Id,
            Username: user.Username,
            Email: null,                    // not in users table minimal mapping
            Type: user.Type,
            Token: string.Empty,            // not stored in DB
            IsOldToken: false,
            MediaProgress: [],              // owned by Person D
            SeriesHideFromContinueListening: [],
            Bookmarks: [],                  // owned by Person D
            IsActive: user.IsActive ?? false,
            IsLocked: false,                // not in minimal mapping
            LastSeen: null,
            CreatedAt: user.CreatedAt.ToUnixTimeMilliseconds(),
            Permissions: permissions,
            LibrariesAccessible: librariesAccessible,
            ItemTagsSelected: itemTagsSelected,
            HasOpenIDLink: false);
    }

    private static (JsonObject permissions, List<string> librariesAccessible, List<string> itemTagsSelected)
        ParsePermissions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (new JsonObject(), [], []);

        try
        {
            var root = JsonDocument.Parse(json).RootElement;

            // Extract and remove the list fields — Node spreads them at the top level
            var librariesAccessible = root.TryGetProperty("librariesAccessible", out var la) && la.ValueKind == JsonValueKind.Array
                ? la.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                : new List<string>();

            var itemTagsSelected = root.TryGetProperty("itemTagsSelected", out var its) && its.ValueKind == JsonValueKind.Array
                ? its.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                : new List<string>();

            // Build permissions object without those two fields
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
