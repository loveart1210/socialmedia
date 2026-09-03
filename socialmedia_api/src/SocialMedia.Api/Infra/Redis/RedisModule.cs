using StackExchange.Redis;

namespace SocialMedia.Api.Infra.Redis;

public static class RedisModule
{
    /// <summary>Tên connection string Redis trong cấu hình.</summary>
    public const string ConnectionStringName = "Redis";

    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Thiếu connection string '{ConnectionStringName}' (ConnectionStrings:{ConnectionStringName}).");
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);

            // Redis chưa lên thì app vẫn khởi động được và /api/health báo unhealthy,
            // thay vì crash lúc start rồi không ai biết vì sao.
            options.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(options);
        });

        return services;
    }
}
