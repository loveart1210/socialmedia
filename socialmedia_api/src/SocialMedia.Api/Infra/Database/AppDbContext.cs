using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SocialMedia.Api.Infra.Database;

/// <summary>
/// DbContext duy nhất của API. Cấu hình entity nằm cùng file với entity trong từng
/// module và được nạp bằng <c>ApplyConfigurationsFromAssembly</c> (api.md mục 1/2).
/// </summary>
public class AppDbContext(DbContextOptions options, TimeProvider timeProvider) : DbContext(options)
{
    private readonly TimeProvider _timeProvider = timeProvider;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySnakeCaseNames(modelBuilder);
        ApplySoftDeleteQueryFilters(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Gán <c>CreatedAt</c>/<c>UpdatedAt</c> tại một chỗ duy nhất; <c>CreatedAt</c> của
    /// bản ghi đã có không bao giờ bị ghi đè.
    /// </summary>
    private void ApplyTimestamps()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Đặt <c>HasQueryFilter(x =&gt; x.DeletedAt == null)</c> cho mọi entity
    /// <see cref="ISoftDeletable"/>. Đọc cả bản ghi đã xoá thì gọi
    /// <c>IgnoreQueryFilters()</c> có chủ đích (ví dụ cây bình luận — BR-08).
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is not null ||
                entityType.IsOwned() ||
                !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt)),
                Expression.Constant(null, typeof(DateTimeOffset?)));

            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Đặt tên bảng/cột mặc định theo snake_case (SPEC mục 3). Tên đã khai tường minh
    /// trong <c>IEntityTypeConfiguration</c> (<c>ToTable</c>, <c>HasColumnName</c>) luôn
    /// thắng — convention này chỉ điền chỗ chưa khai, để không phải lặp lại
    /// <c>HasColumnName</c> cho từng cột.
    /// </summary>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindAnnotation(RelationalAnnotationNames.TableName) is null &&
                entityType.GetTableName() is { } tableName)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    /// <summary><c>CreatedAt</c> → <c>created_at</c>, <c>UserID</c> → <c>user_id</c>.</summary>
    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (char.IsUpper(current))
            {
                var previousIsLower = i > 0 && !char.IsUpper(name[i - 1]) && name[i - 1] != '_';
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (i > 0 && name[i - 1] != '_' && (previousIsLower || nextIsLower))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLower(current, CultureInfo.InvariantCulture));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
