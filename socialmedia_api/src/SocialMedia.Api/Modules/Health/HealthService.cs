using Microsoft.EntityFrameworkCore;
using SocialMedia.Api.Infra.Database;
using SocialMedia.Api.Modules.Health.Dtos;
using StackExchange.Redis;

namespace SocialMedia.Api.Modules.Health;

/// <summary>
/// Kiểm tra API có nói chuyện được với PostgreSQL và Redis không. Hai phụ thuộc
/// này đủ để kết luận API phục vụ được request nghiệp vụ.
/// </summary>
public sealed class HealthService(
    AppDbContext db,
    IConnectionMultiplexer redis,
    ILogger<HealthService> logger)
{
    public async Task<HealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var postgres = await CheckAsync("PostgreSQL", () => db.Database.CanConnectAsync(cancellationToken));
        var redisCheck = await CheckAsync("Redis", async () =>
        {
            await redis.GetDatabase().PingAsync();
            return true;
        });

        var checks = new Dictionary<string, HealthCheckResult>
        {
            ["postgres"] = postgres,
            ["redis"] = redisCheck,
        };

        var status = checks.Values.All(c => c.Status == HealthResponse.Healthy)
            ? HealthResponse.Healthy
            : HealthResponse.Unhealthy;

        return new HealthResponse(status, checks);
    }

    private async Task<HealthCheckResult> CheckAsync(string name, Func<Task<bool>> probe)
    {
        try
        {
            return await probe()
                ? new HealthCheckResult(HealthResponse.Healthy, null)
                : new HealthCheckResult(HealthResponse.Unhealthy, $"{name} không phản hồi.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check {Dependency} thất bại", name);
            return new HealthCheckResult(HealthResponse.Unhealthy, ex.Message);
        }
    }
}
