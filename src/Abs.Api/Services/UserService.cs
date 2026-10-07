using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

/// <summary>DB queries for the users table. Node owns the schema — no writes here.</summary>
public class UserService(AbsDbContext db)
{
    public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
}
