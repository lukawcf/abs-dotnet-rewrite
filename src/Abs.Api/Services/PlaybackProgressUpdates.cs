using System.Text.Json;
using System.Text.Json.Nodes;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

public record ProgressWriteResult(int Status, string Message);

public partial class PlaybackProgressService
{
    // Node User.js:730-830. Missing item/episode errors occur before mutation.
    public async Task<ProgressWriteResult> UpsertAsync(
        string userId, string itemId, string? episodeId, JsonElement patch, CancellationToken ct)
    {
        foreach (var name in new[] { "duration", "currentTime", "progress", "ebookProgress", "markAsFinishedPercentComplete", "markAsFinishedTimeRemaining" })
        {
            if (patch.TryGetProperty(name, out var scalar) && scalar.ValueKind != JsonValueKind.Null &&
                ProgressPatchValues.Number(scalar) is null) return new(501, "Unsupported progress value");
        }
        foreach (var name in new[] { "isFinished", "hideFromContinueListening" })
        {
            if (patch.TryGetProperty(name, out var flag) && !ProgressPatchValues.BooleanScalar(flag))
                return new(501, "Unsupported progress value");
        }
        foreach (var name in new[] { "createdAt", "finishedAt" })
        {
            if (patch.TryGetProperty(name, out var date) && ProgressPatchValues.Truth(date) &&
                ProgressPatchValues.Date(date) is null) return new(501, "Unsupported progress value");
        }
        if (patch.TryGetProperty("extraData", out _)) return new(501, "Unsupported progress value");
        string? mediaId;
        string? podcastId = null;
        if (episodeId is not null)
        {
            var episode = await db.Set<ProgressEpisodeLookup>().FirstOrDefaultAsync(x => x.Id == episodeId, ct);
            if (episode is null) return new(404, "Episode not found");
            mediaId = episode.Id;
            podcastId = episode.PodcastId;
        }
        else
        {
            var item = await db.Set<ProgressItemLookup>().FirstOrDefaultAsync(x => x.Id == itemId, ct);
            if (item is null) return new(404, "Library item not found");
            if (item.MediaType != "book") return new(400, "Library item is not a book");
            if (!await db.Set<ProgressBookLookup>().AnyAsync(x => x.Id == item.MediaId, ct))
                return new(501, "Book media is unavailable");
            mediaId = item.MediaId;
        }

        // Require a real user even though the development header accepts arbitrary IDs.
        if (!await db.Users.AnyAsync(x => x.Id == userId, ct)) return new(403, "Forbidden");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Set<PlaybackProgress>().AsTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MediaItemId == mediaId, ct);
        var now = DateTimeOffset.UtcNow;
        if (row is null)
        {
            row = new PlaybackProgress
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                MediaItemId = mediaId,
                MediaItemType = episodeId is null ? "book" : "podcastEpisode",
                PodcastId = podcastId,
                Duration = CreationNumber(patch, "duration"),
                CurrentTime = CreationNumber(patch, "currentTime"),
                IsFinished = HasTruth(patch, "isFinished"),
                HideFromContinueListening = HasTruth(patch, "hideFromContinueListening"),
                EbookLocation = patch.TryGetProperty("ebookLocation", out var location) && ProgressPatchValues.Truth(location)
                    ? location.ToString() : null,
                EbookProgress = CreationNumber(patch, "ebookProgress"),
                CreatedAt = patch.TryGetProperty("createdAt", out var created) && ProgressPatchValues.Truth(created)
                    ? ProgressPatchValues.Date(created) ?? now : now,
                UpdatedAt = now,
                ExtraData = new JsonObject { ["libraryItemId"] = itemId, ["progress"] = CreationNumber(patch, "progress") }.ToJsonString()
            };
            if (row.IsFinished == true)
            {
                row.FinishedAt = patch.TryGetProperty("finishedAt", out var finished) && ProgressPatchValues.Truth(finished)
                    ? ProgressPatchValues.Date(finished) ?? now : now;
                var extra = JsonNode.Parse(row.ExtraData)!.AsObject();
                extra["progress"] = 1;
                row.ExtraData = extra.ToJsonString();
            }
            db.Add(row);
        }
        else
        {
            var forceExtraUpdate = ApplyUpdate(row, patch, now);
            db.ChangeTracker.DetectChanges();
            if (forceExtraUpdate) db.Entry(row).Property(x => x.ExtraData).IsModified = true;
            if (db.Entry(row).State == EntityState.Modified) row.UpdatedAt = now;
            // Node applies lastUpdate after save, including no-op updates; creation ignores it.
            if (patch.TryGetProperty("lastUpdate", out var last) && ProgressPatchValues.Truth(last) &&
                ProgressPatchValues.Date(last) is { } sync) row.UpdatedAt = sync;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(200, "OK");
    }

    private static bool HasTruth(JsonElement p, string name) =>
        p.TryGetProperty(name, out var v) && ProgressPatchValues.Truth(v);
    private static double CreationNumber(JsonElement p, string name) =>
        p.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? ProgressPatchValues.Number(v) ?? 0 : 0;

    // Node MediaProgress.js:189-270; existing createdAt is not changed by Sequelize save.
    private static bool ApplyUpdate(PlaybackProgress row, JsonElement p, DateTimeOffset now)
    {
        var forceExtraUpdate = false;
        var extra = JsonNode.Parse(row.ExtraData ?? "{}") as JsonObject ?? new JsonObject();
        var oldTime = row.CurrentTime;
        var wasFinished = row.IsFinished == true;
        var hasFinished = p.TryGetProperty("isFinished", out var finish);
        var unfinish = hasFinished && !ProgressPatchValues.Truth(finish) && wasFinished;
        var finishTransition = hasFinished && ProgressPatchValues.Truth(finish) && !wasFinished;
        if (finishTransition)
        {
            forceExtraUpdate = true;
            row.FinishedAt = p.TryGetProperty("finishedAt", out var date) && ProgressPatchValues.Truth(date)
                ? ProgressPatchValues.Date(date) ?? now : now;
            extra["progress"] = 1;
        }
        else if (unfinish)
        {
            forceExtraUpdate = true;
            row.FinishedAt = null;
            row.CurrentTime = 0;
            extra["progress"] = 0;
        }
        else if (!hasFinished && p.TryGetProperty("progress", out var progress) &&
                 ProgressPatchValues.Number(progress) is { } n &&
                 (progress.ValueKind != JsonValueKind.Number || n != Ratio(row)))
        {
            extra["progress"] = Math.Clamp(n, 0, 1);
            forceExtraUpdate = true;
        }

        if (p.TryGetProperty("duration", out var duration)) row.Duration = NullableNumber(duration);
        if (!unfinish && p.TryGetProperty("currentTime", out var time)) row.CurrentTime = NullableNumber(time);
        if (hasFinished) row.IsFinished = ProgressPatchValues.StoredBoolean(finish);
        if (p.TryGetProperty("hideFromContinueListening", out var hidden))
            row.HideFromContinueListening = ProgressPatchValues.StoredBoolean(hidden);
        if (p.TryGetProperty("ebookLocation", out var location)) row.EbookLocation = location.ValueKind == JsonValueKind.Null ? null : location.ToString();
        if (p.TryGetProperty("ebookProgress", out var ebook)) row.EbookProgress = NullableNumber(ebook);
        if (!unfinish && !finishTransition && p.TryGetProperty("finishedAt", out var at))
            row.FinishedAt = at.ValueKind == JsonValueKind.Null ? null : ProgressPatchValues.Date(at);
        var changedTime = row.CurrentTime != oldTime;
        if (changedTime && !HasTruth(p, "hideFromContinueListening")) row.HideFromContinueListening = false;
        var shouldFinish = false;
        if (row.Duration is { } d && d != 0)
        {
            var percent = p.TryGetProperty("markAsFinishedPercentComplete", out var threshold) &&
                threshold.ValueKind != JsonValueKind.Null ? ProgressPatchValues.Number(threshold) : null;
            var remaining = p.TryGetProperty("markAsFinishedTimeRemaining", out var r) &&
                r.ValueKind != JsonValueKind.Null ? ProgressPatchValues.Number(r) ?? 10 : 10;
            shouldFinish = percent is > 0 ? percent / 100 < Ratio(row) : d - (row.CurrentTime ?? 0) < remaining;
        }
        if (row.IsFinished != true && shouldFinish)
        {
            forceExtraUpdate = true;
            row.IsFinished = true;
            row.FinishedAt ??= now;
            extra["progress"] = 1;
        }
        else if (row.IsFinished == true && changedTime && !shouldFinish)
        {
            row.IsFinished = false;
            row.FinishedAt = null;
        }
        var newExtra = extra.ToJsonString();
        // Preserve the original text for no-op updates rather than dirtying EF on whitespace alone.
        if (!JsonNode.DeepEquals(JsonNode.Parse(row.ExtraData ?? "{}"), extra)) row.ExtraData = newExtra;
        return forceExtraUpdate;
    }

    private static double? NullableNumber(JsonElement v) =>
        v.ValueKind == JsonValueKind.Null ? null : ProgressPatchValues.Number(v);
    private static double Ratio(PlaybackProgress row) =>
        row.Duration is { } d && d != 0 ? Math.Clamp((row.CurrentTime ?? 0) / d, 0, 1) : 0;
}
