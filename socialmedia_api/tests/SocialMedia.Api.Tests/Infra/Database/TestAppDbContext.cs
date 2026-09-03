using Microsoft.EntityFrameworkCore;
using SocialMedia.Api.Infra.Database;

namespace SocialMedia.Api.Tests.Infra.Database;

/// <summary>
/// Entity giả chỉ tồn tại trong test, dùng để kiểm hai convention của
/// <see cref="AppDbContext"/>: tự gán timestamps và global query filter xoá mềm.
/// </summary>
public sealed class FakeEntity : IHasTimestamps, ISoftDeletable
{
    public Guid Id { get; set; }

    public string DisplayLabel { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// Đăng ký <see cref="FakeEntity"/> TRƯỚC khi gọi <c>base.OnModelCreating</c> để
/// convention của <see cref="AppDbContext"/> nhìn thấy nó — đúng thứ tự mà entity
/// thật của các module sẽ đi qua.
/// </summary>
public sealed class TestAppDbContext(DbContextOptions<TestAppDbContext> options, TimeProvider timeProvider)
    : AppDbContext(options, timeProvider)
{
    public DbSet<FakeEntity> FakeEntities => Set<FakeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FakeEntity>(entity =>
        {
            entity.ToTable("fake_entities");
            entity.HasKey(x => x.Id);
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>TimeProvider đứng yên — để khẳng định giá trị timestamps thay vì đoán khoảng.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
