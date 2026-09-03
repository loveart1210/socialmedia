using Microsoft.EntityFrameworkCore;
using Npgsql;
using SocialMedia.Api.Tests.Infrastructure;

namespace SocialMedia.Api.Tests.Infra.Database;

/// <summary>
/// Convention của AppDbContext chạy trên Postgres thật (không InMemory): global query
/// filter và tên cột snake_case chỉ chứng minh được ở đúng provider sẽ dùng ở prod.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AppDbContextConventionsTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset CreatedMoment = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly FixedTimeProvider _clock = new(CreatedMoment);
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        // Mỗi lớp test một database riêng để bảng giả không lẫn vào DB của test API.
        var builder = new NpgsqlConnectionStringBuilder(factory.PostgresConnectionString);
        var databaseName = $"conventions_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var command = admin.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        _connectionString = builder.ConnectionString;

        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Them_moi_thi_CreatedAt_va_UpdatedAt_duoc_gan_tu_dong()
    {
        var id = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            db.FakeEntities.Add(new FakeEntity { Id = id, DisplayLabel = "a" });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var saved = await db.FakeEntities.SingleAsync(x => x.Id == id);
            Assert.Equal(CreatedMoment, saved.CreatedAt);
            Assert.Equal(CreatedMoment, saved.UpdatedAt);
        }
    }

    [Fact]
    public async Task Sua_thi_chi_UpdatedAt_doi_con_CreatedAt_giu_nguyen()
    {
        var id = Guid.NewGuid();
        var updatedMoment = CreatedMoment.AddHours(6);

        await using (var db = CreateContext())
        {
            db.FakeEntities.Add(new FakeEntity { Id = id, DisplayLabel = "trước" });
            await db.SaveChangesAsync();
        }

        _clock.Now = updatedMoment;

        await using (var db = CreateContext())
        {
            var entity = await db.FakeEntities.SingleAsync(x => x.Id == id);
            entity.DisplayLabel = "sau";
            entity.CreatedAt = updatedMoment; // cố tình sửa — convention phải bỏ qua
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var saved = await db.FakeEntities.SingleAsync(x => x.Id == id);
            Assert.Equal(CreatedMoment, saved.CreatedAt);
            Assert.Equal(updatedMoment, saved.UpdatedAt);
        }
    }

    [Fact]
    public async Task Query_filter_an_ban_ghi_da_xoa_mem_tru_khi_IgnoreQueryFilters()
    {
        var id = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            db.FakeEntities.Add(new FakeEntity { Id = id, DisplayLabel = "sẽ xoá" });
            await db.SaveChangesAsync();

            var entity = await db.FakeEntities.SingleAsync(x => x.Id == id);
            entity.DeletedAt = _clock.GetUtcNow();
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            Assert.False(await db.FakeEntities.AnyAsync(x => x.Id == id));
            Assert.True(await db.FakeEntities.IgnoreQueryFilters().AnyAsync(x => x.Id == id));
        }
    }

    [Fact]
    public async Task Cot_duoc_dat_ten_snake_case_theo_SPEC()
    {
        await using var db = CreateContext();

        var columns = await db.Database
            .SqlQuery<string>($@"
                SELECT column_name AS ""Value""
                FROM information_schema.columns
                WHERE table_name = 'fake_entities'")
            .ToListAsync();

        Assert.Contains("created_at", columns);
        Assert.Contains("updated_at", columns);
        Assert.Contains("deleted_at", columns);
        Assert.Contains("display_label", columns);
    }

    private TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new TestAppDbContext(options, _clock);
    }
}
