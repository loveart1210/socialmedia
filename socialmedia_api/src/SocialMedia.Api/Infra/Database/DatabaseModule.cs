using Microsoft.EntityFrameworkCore;

namespace SocialMedia.Api.Infra.Database;

public static class DatabaseModule
{
    /// <summary>Tên connection string PostgreSQL trong cấu hình.</summary>
    public const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Thiếu connection string '{ConnectionStringName}' (ConnectionStrings:{ConnectionStringName}).");
        }

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        return services;
    }
}
