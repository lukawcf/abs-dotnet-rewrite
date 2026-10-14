using System.Security.Claims;
using System.Text.Json;
using Abs.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

public partial class BookmarksController
{
    // Node MeController.js:327-435: existence and access precede payload validation.
    [HttpPost("/api/me/item/{id}/bookmark")]
    public Task<IActionResult> Create(string id, [FromBody] JsonElement payload, CancellationToken ct) =>
        Write(id, payload, BookmarkOperation.Create, ct);

    [HttpPatch("/api/me/item/{id}/bookmark")]
    public Task<IActionResult> Update(string id, [FromBody] JsonElement payload, CancellationToken ct) =>
        Write(id, payload, BookmarkOperation.Update, ct);

    private async Task<IActionResult> Write(string id, JsonElement payload, BookmarkOperation operation, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ProgressWrites:Enabled")) return StatusCode(501);
        var denied = await CheckWriteAccess(id, ct);
        if (denied is not null) return denied;
        if (payload.ValueKind != JsonValueKind.Object) return Plain(400, "Invalid time");
        if (!payload.TryGetProperty("time", out var time) || time.ValueKind == JsonValueKind.Null ||
            ProgressPatchValues.Number(time) is null) return Plain(400, "Invalid time");
        if (!payload.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(title.GetString())) return Plain(400, "Invalid title");
        var result = await bookmarks.WriteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, time,
            title.GetString(), operation, ct);
        return result.Bookmark is { } bookmark ? Ok(bookmark) : Plain(result.Status, result.Message);
    }

    [HttpDelete("/api/me/item/{id}/bookmark/{time}")]
    public async Task<IActionResult> Delete(string id, string time, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ProgressWrites:Enabled")) return StatusCode(501);
        var denied = await CheckWriteAccess(id, ct);
        if (denied is not null) return denied;
        var numeric = ProgressPatchValues.Number(JsonSerializer.SerializeToElement(time));
        if (numeric is null) return Plain(400, "Invalid time");
        var result = await bookmarks.WriteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id,
            JsonSerializer.SerializeToElement(numeric.Value), null, BookmarkOperation.Delete, ct);
        return Plain(result.Status, result.Message);
    }

    private async Task<IActionResult?> CheckWriteAccess(string id, CancellationToken ct)
    {
        var item = await bookmarks.GetItemAsync(id, ct);
        if (item is null) return Plain(404, "Not Found");
        var user = await bookmarks.GetUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
        return user is null || item.LibraryId is null ||
            !await permissions.CanAccessLibraryAsync(item.LibraryId, ct) || !CanAccessMedia(user, item)
            ? Plain(403, "Forbidden") : null;
    }
}
