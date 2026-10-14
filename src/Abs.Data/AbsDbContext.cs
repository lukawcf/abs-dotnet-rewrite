using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Data;

/// <summary>
/// Read-only view of the database that Node (audiobookshelf) owns.
/// Never call Migrate or EnsureCreated, and never generate migrations.
/// </summary>
public class AbsDbContext(DbContextOptions<AbsDbContext> options) : DbContext(options)
{
    public DbSet<Library> Libraries => Set<Library>();
    public DbSet<LibraryFolder> LibraryFolders => Set<LibraryFolder>();
    public DbSet<User> Users => Set<User>();
    public DbSet<LibraryItem> LibraryItems => Set<LibraryItem>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BookSeries> BookSeries => Set<BookSeries>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbsDbContext).Assembly);
}
