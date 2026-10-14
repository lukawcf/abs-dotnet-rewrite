using System.Security.Claims;
using System.Text.Json;
using Abs.Api.Abstractions;
using Abs.Api.Dtos;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/bookmarks")]
public partial class BookmarksController(BookmarkService bookmarks, IPermissionService permissions, IConfiguration configuration) : ControllerBase
{
    // Node MeController.js:123-126: no item permission filtering on the all-bookmarks endpoint.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var user = await bookmarks.GetUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
        return user is null ? Plain(403, "Forbidden") : Ok(new BookmarksResponse(user.Bookmarks));
    }

    // Node MeController.js:134-146; User.js:658-682.
    [HttpGet("{libraryItemId}")]
    public async Task<IActionResult> GetForItem(string libraryItemId, CancellationToken ct)
    {
        var item = await bookmarks.GetItemAsync(libraryItemId, ct);
        if (item is null) return Plain(404, "Not Found");
        var user = await bookmarks.GetUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
        if (user is null || item.LibraryId is null ||
            !await permissions.CanAccessLibraryAsync(item.LibraryId, ct) || !CanAccessMedia(user, item))
            return Plain(403, "Forbidden");
        return Ok(BookmarkService.ForItem(user, libraryItemId));
    }

    private static bool CanAccessMedia(BookmarkUserData user, BookmarkItemData item)
    {
        using var document = JsonDocument.Parse(user.Permissions ?? "{}");
        var p = document.RootElement;
        bool Flag(string key) => p.ValueKind == JsonValueKind.Object && p.TryGetProperty(key, out var v) &&
            v.ValueKind == JsonValueKind.True;
        if (item.Explicit && !(user.IsActive && Flag("accessExplicitContent"))) return false;
        if (Flag("accessAllTags")) return true;
        var selected = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("itemTagsSelected", out var list) &&
            list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
        return Flag("selectedTagsNotAccessible")
            ? item.Tags.All(x => !selected.Contains(x)) : selected.Any(item.Tags.Contains);
    }

    private static ContentResult Plain(int status, string message) =>
        new() { StatusCode = status, Content = message, ContentType = "text/plain" };
}
