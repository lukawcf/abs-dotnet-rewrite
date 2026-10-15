using System.Text.Json;
using Abs.Api.Dtos;
using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

/// <summary>DB queries for the users table. Node owns the schema, so there are no writes here.</summary>
public class UserService(AbsDbContext db, PlaybackProgressService progress, BookmarkService bookmarks)
{
    public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    /// <summary>Node's findAll has no ORDER BY, so rows come back in insertion order; createdAt is the closest key.</summary>
    public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>The full browser user (GET /api/me): includes mediaProgress and bookmarks.</summary>
    public async Task<UserDto?> GetBrowserUserAsync(string userId, bool isOldToken, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return null;

        var rows = await progress.GetAllAsync(user.Id, cancellationToken);
        var bookmarkData = await bookmarks.GetUserAsync(user.Id, cancellationToken);
        IReadOnlyList<JsonElement> list = bookmarkData?.Bookmarks ?? [];
        return UserDto.From(user, hideRootToken: false, minimal: false, isOldToken, rows, list);
    }

    /// <summary>The minimal browser users (GET /api/users).</summary>
    public async Task<List<UserDto>> GetMinimalUsersAsync(bool hideRootToken, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.Select(u => UserDto.From(u, hideRootToken, minimal: true, isOldToken: false, [], [])).ToList();
    }
}
