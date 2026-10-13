using System.Text.Json;
using Abs.Api.Dtos;
using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

public record BookmarkUserData(string? Permissions, bool IsActive, IReadOnlyList<JsonElement> Bookmarks);
public record BookmarkItemData(string? LibraryId, bool Explicit, IReadOnlyList<string> Tags);

public partial class BookmarkService(AbsDbContext db)
{
    public async Task<BookmarkUserData?> GetUserAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.Where(x => x.Id == userId)
            .Select(x => new { x.Permissions, x.IsActive, Bookmarks = EF.Property<string?>(x, "DBookmarks") })
            .FirstOrDefaultAsync(ct);
        if (user is null) return null;
        using var json = JsonDocument.Parse(user.Bookmarks ?? "[]");
        var bookmarks = json.RootElement.ValueKind == JsonValueKind.Array
            ? json.RootElement.EnumerateArray().Select(x => x.Clone()).ToArray() : [];
        return new(user.Permissions, user.IsActive == true, bookmarks);
    }

    public async Task<BookmarkItemData?> GetItemAsync(string itemId, CancellationToken ct)
    {
        var item = await db.Set<ProgressItemLookup>().FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return null;
        bool isExplicit;
        string? tags;
        if (item.MediaType == "book")
        {
            var book = await db.Set<ProgressBookLookup>().FirstOrDefaultAsync(x => x.Id == item.MediaId, ct);
            if (book is null) return null;
            isExplicit = book.Explicit == true; tags = book.Tags;
        }
        else if (item.MediaType == "podcast")
        {
            var podcast = await db.Set<BookmarkPodcastLookup>().FirstOrDefaultAsync(x => x.Id == item.MediaId, ct);
            if (podcast is null) return null;
            isExplicit = podcast.Explicit == true; tags = podcast.Tags;
        }
        else return null;
        using var parsed = JsonDocument.Parse(tags ?? "[]");
        var values = parsed.RootElement.ValueKind == JsonValueKind.Array
            ? parsed.RootElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
        return new(item.LibraryId, isExplicit, values);
    }

    public static BookmarksResponse ForItem(BookmarkUserData user, string itemId) =>
        new(user.Bookmarks.Where(x => x.ValueKind == JsonValueKind.Object &&
            x.TryGetProperty("libraryItemId", out var id) && id.ValueKind == JsonValueKind.String &&
            id.GetString() == itemId).ToArray());
}
