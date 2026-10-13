using Abs.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Abs.Api.Tests;

/// <summary>
/// Runs the API against an in-memory SQLite database built from Fixtures/schema.sql and seed.sql.
/// The live database is never used.
/// </summary>
public class AbsApiFactory : WebApplicationFactory<Program>
{
    public const string LibraryA = "aeb8a59a-eb09-4444-bd9d-2ca5cf8fa161";
    public const string LibraryB = "bbbbbbbb-0000-4000-8000-000000000002";

    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public string EnvironmentName { get; init; } = "Development";
    public bool EnableProgressWrites { get; init; }
    public string? TestJwtSecret { get; init; }

    public Action<IServiceCollection>? ConfigureTestServices { get; init; }

    public AbsApiFactory()
    {
        connection.Open();
        foreach (var file in new[] { "schema.sql", "seed.sql", "w2-progress-schema.sql", "w2-progress-seed.sql", "w2-write-media-schema.sql", "w2-write-media-seed.sql", "w2-bookmark-seed.sql" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));
            command.ExecuteNonQuery();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProgressWrites:Enabled"] = EnableProgressWrites.ToString(),
                ["JWT_SECRET_KEY"] = TestJwtSecret
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AbsDbContext>>();
            services.AddDbContext<AbsDbContext>(options => options
                .UseSqlite(connection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
            ConfigureTestServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
