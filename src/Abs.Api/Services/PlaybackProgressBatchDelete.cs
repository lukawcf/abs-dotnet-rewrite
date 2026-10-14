using System.Text.Json;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

public partial class PlaybackProgressService
{
    // Node MeController.js:296-320: sequential per-entry writes; returned item errors are skipped.
    // Earlier successes survive later failures. Database exceptions still propagate, as in Node.
    public async Task BatchUpdateAsync(string userId, JsonElement payload, CancellationToken ct)
    {
        foreach (var entry in payload.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("libraryItemId", out var item) || item.ValueKind != JsonValueKind.String)
                continue;
            var episode = entry.TryGetProperty("episodeId", out var value) && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(value.GetString()) ? value.GetString() : null;
            db.ChangeTracker.Clear();
            await UpsertAsync(userId, item.GetString()!, episode, entry, ct);
        }
    }

    // Node MeController.js:253-265: id is a progress row ID, not a library item ID.
    // Filter ownership in the DELETE itself to avoid a separate check/delete race.
    public async Task<bool> DeleteAsync(string userId, string progressId, CancellationToken ct) =>
        await db.Set<PlaybackProgress>().Where(x => x.Id == progressId && x.UserId == userId)
            .ExecuteDeleteAsync(ct) != 0;
}
