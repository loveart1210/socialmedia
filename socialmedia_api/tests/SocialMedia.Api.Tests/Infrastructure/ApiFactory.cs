using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialMedia.Api.Infra.Database;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace SocialMedia.Api.Tests.Infrastructure;

/// <summary>
/// Dựng API thật trên PostgreSQL và Redis thật (Testcontainers), mỗi lần chạy một
/// container riêng — không đụng DB dev và không dùng InMemory provider, vì global
/// query filter, CHECK constraint và partial index chỉ đúng trên Postgres (api.md mục 9).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Cùng image với docker-compose để test không "xanh trên bản Postgres khác".
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16.15-alpine")
        .WithDatabase("socialmedia_test")
        .WithUsername("socialmedia")
        .WithPassword("socialmedia_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    /// <summary>Connection string tới Postgres của lần chạy này (dùng cho test tầng DbContext).</summary>
    public string PostgresConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Truy cập Services lần đầu mới dựng host — phải sau khi container đã có cổng.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Phase 0 chưa có migration nào; từ Phase 1.1 trở đi khối này áp toàn bộ migration.
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (không phải ConfigureAppConfiguration): Program.cs đọc cấu hình
        // ngay trong lúc dựng builder, còn callback ConfigureAppConfiguration chỉ chạy
        // lúc Build() nên tới muộn. Vì thế appsettings.json cũng không khai sẵn các khóa
        // này — xem src/SocialMedia.Api/README.settings.md.
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", TestJwt.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwt.Audience);
        builder.UseSetting("Jwt:SecretKey", TestJwt.SecretKey);
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");

        // Nạp controller chỉ-dùng-cho-test (ProtectedProbeController) vào app thật.
        builder.ConfigureTestServices(services => services
            .AddControllers()
            .AddApplicationPart(typeof(ApiFactory).Assembly));
    }

    /// <summary>HttpClient kèm Bearer token hợp lệ.</summary>
    public HttpClient CreateAuthenticatedClient(Guid? userId = null, string role = "User")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                TestJwt.CreateAccessToken(userId, role));
        return client;
    }
}

/// <summary>Một bộ container dùng chung cho toàn bộ test tích hợp.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
