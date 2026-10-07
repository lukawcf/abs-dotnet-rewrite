using Microsoft.EntityFrameworkCore;

namespace Abs.Data;

/// <summary>
/// Read-only view of the database that Node (audiobookshelf) owns.
/// Never call Migrate or EnsureCreated, and never generate migrations.
/// </summary>
public class AbsDbContext(DbContextOptions<AbsDbContext> options) : DbContext(options)
{
}
