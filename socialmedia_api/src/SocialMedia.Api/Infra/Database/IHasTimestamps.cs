namespace SocialMedia.Api.Infra.Database;

/// <summary>
/// Entity có <c>created_at</c>/<c>updated_at</c>. Hai cột này do
/// <see cref="AppDbContext"/> gán — entity
/// và service không bao giờ tự gán (api.md mục 2).
/// </summary>
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}
