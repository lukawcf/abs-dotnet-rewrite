using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.Data;

public static class DataServiceCollectionExtensions
{
    public const string ConnectionStringName = "Abs";

    public static IServiceCollection AddAbsData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        // The file is opened lazily, so the service starts even when the database is not mounted.
        services.AddDbContext<AbsDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }
}
