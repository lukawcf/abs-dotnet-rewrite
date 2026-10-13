using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

public enum BookmarkOperation { Create, Update, Delete }
public record BookmarkWriteResult(int Status, string Message, JsonElement? Bookmark = null);

public partial class BookmarkService
{
    // Node User.js:841-918. Mutate only this user's bookmark JSON and updatedAt.
    public async Task<BookmarkWriteResult> WriteAsync(string userId, string itemId, JsonElement time,
        string? title, BookmarkOperation operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.AsTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return new(403, "Forbidden");
        var entry = db.Entry(user);
        var stored = entry.Property<string?>("DBookmarks");
        if (stored.CurrentValue is null || JsonNode.Parse(stored.CurrentValue) is not JsonArray rows)
            return new(501, "Unsupported bookmark data");

        var match = rows.OfType<JsonObject>().FirstOrDefault(x => ItemMatches(x, itemId) &&
            x["time"] is { } value && LooselyEquals(JsonSerializer.SerializeToElement(value), time));
        if (match is null && operation != BookmarkOperation.Create) return new(404, "Not Found");
        var save = true;
        JsonObject? result = match;
        if (operation == BookmarkOperation.Delete)
        {
            // Node's find uses ==, while remove filters with !==: string-time entries may remain.
            for (var i = rows.Count - 1; i >= 0; i--)
                if (rows[i] is JsonObject row && ItemMatches(row, itemId) && row["time"] is { } value &&
                    StrictlyEquals(JsonSerializer.SerializeToElement(value), time)) rows.RemoveAt(i);
        }
        else if (match is not null)
        {
            var previous = match["title"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            save = operation == BookmarkOperation.Update || previous != title;
            if (save) match["title"] = title;
        }
        else
        {
            result = new JsonObject
            {
                ["libraryItemId"] = itemId,
                ["time"] = JsonNode.Parse(time.GetRawText()),
                ["title"] = title,
                ["createdAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            rows.Add(result);
        }
        if (save)
        {
            stored.CurrentValue = rows.ToJsonString();
            stored.IsModified = true; // Node forces JSON dirty for PATCH and DELETE, even if unchanged.
            entry.Property<DateTimeOffset>("DBookmarkUpdatedAt").CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(200, "OK", operation == BookmarkOperation.Delete ? null : JsonSerializer.SerializeToElement(result));
    }

    private static bool ItemMatches(JsonObject row, string itemId) =>
        row["libraryItemId"] is JsonValue id && id.TryGetValue<string>(out var text) && text == itemId;

    private static bool StrictlyEquals(JsonElement a, JsonElement b) => a.ValueKind == b.ValueKind &&
        a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetDouble() == b.GetDouble(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => false
        };

    private static bool LooselyEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == b.ValueKind) return StrictlyEquals(a, b);
        if (a.ValueKind is JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array) return false;
        return ProgressPatchValues.Number(a) is { } n && ProgressPatchValues.Number(b) == n;
    }
}
