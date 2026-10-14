using System.Security.Claims;
using System.Text.Json;
using Abs.Api.Abstractions;
using Abs.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

/// <summary>Reads users.permissions (JSON written by Node) for the user in the current request.</summary>
public class PermissionService(AbsDbContext db, IHttpContextAccessor httpContextAccessor) : IPermissionService
{
    public async Task<bool> CanAccessLibraryAsync(string libraryId, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        var permissions = user.Value.Permissions;
        if (permissions.TryGetProperty("accessAllLibraries", out var all) && all.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        return ReadLibrariesAccessible(permissions).Contains(libraryId);
    }

    public async Task<IReadOnlyList<string>?> GetLibrariesAccessibleAsync(CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(cancellationToken);
        return user is null ? null : ReadLibrariesAccessible(user.Value.Permissions);
    }

    public async Task<ItemAccessRules?> GetItemAccessRulesAsync(CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var permissions = user.Value.Permissions;
        var explicitAllowed = ReadFlag(permissions, "accessExplicitContent") && user.Value.IsActive;
        var tags = permissions.TryGetProperty("itemTagsSelected", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];
        return new ItemAccessRules(
            explicitAllowed,
            ReadFlag(permissions, "accessAllTags"),
            ReadFlag(permissions, "selectedTagsNotAccessible"),
            tags);
    }

    private static bool ReadFlag(JsonElement permissions, string name) =>
        permissions.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static List<string> ReadLibrariesAccessible(JsonElement permissions) =>
        permissions.TryGetProperty("librariesAccessible", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];

    private readonly record struct LoadedUser(JsonElement Permissions, bool IsActive);

    private async Task<LoadedUser?> LoadUserAsync(CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var row = await db.Users.Where(x => x.Id == userId).Select(x => new { x.Permissions, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        // A user row without permissions has no library access, same as Node.
        return row?.Permissions is null
            ? null
            : new LoadedUser(JsonDocument.Parse(row.Permissions).RootElement.Clone(), row.IsActive == true);
    }
}
