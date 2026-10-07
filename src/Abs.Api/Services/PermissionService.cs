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
        var permissions = await LoadPermissionsAsync(cancellationToken);
        if (permissions is null)
        {
            return false;
        }

        if (permissions.Value.TryGetProperty("accessAllLibraries", out var all) && all.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        return ReadLibrariesAccessible(permissions.Value).Contains(libraryId);
    }

    public async Task<IReadOnlyList<string>?> GetLibrariesAccessibleAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await LoadPermissionsAsync(cancellationToken);
        return permissions is null ? null : ReadLibrariesAccessible(permissions.Value);
    }

    private static List<string> ReadLibrariesAccessible(JsonElement permissions) =>
        permissions.TryGetProperty("librariesAccessible", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];

    private async Task<JsonElement?> LoadPermissionsAsync(CancellationToken cancellationToken)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var json = await db.Users.Where(x => x.Id == userId).Select(x => x.Permissions)
            .FirstOrDefaultAsync(cancellationToken);
        // A user row without permissions has no library access, same as Node.
        return json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
    }
}
