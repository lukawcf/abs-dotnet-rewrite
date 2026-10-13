using Abs.Api.Dtos;
using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

public partial class PlaybackProgressService(AbsDbContext db)
{
    // Node User.js:716-721 matches libraryItemId in extraData, optionally mediaItemId.
    // No library permission filtering: these endpoints only expose the authenticated user's rows.
    public async Task<IReadOnlyList<PlaybackProgressDto>> GetAllAsync(string userId, CancellationToken ct)
    {
        var rows = await db.Set<PlaybackProgress>().AsNoTracking()
            .Where(x => x.UserId == userId).ToListAsync(ct);
        return rows.Select(PlaybackProgressDto.From).ToList();
    }

    public async Task<PlaybackProgressDto?> GetAsync(
        string userId, string itemId, string? episodeId, CancellationToken ct)
    {
        var rows = await GetAllAsync(userId, ct);
        return rows.FirstOrDefault(x => x.LibraryItemId is { ValueKind: System.Text.Json.JsonValueKind.String } id &&
            id.GetString() == itemId && (episodeId is null || x.MediaItemId == episodeId));
    }
}
