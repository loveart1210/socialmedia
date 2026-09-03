namespace SocialMedia.Api.Infra.Database;

/// <summary>
/// Entity xoá mềm bằng <c>deleted_at</c> — <see cref="AppDbContext"/> tự đặt global
/// query filter <c>DeletedAt == null</c> cho mọi entity khai interface này.
/// </summary>
/// <remarks>
/// CHỈ dùng cho entity mà SPEC KHÔNG định nghĩa cột <c>status</c>. Các bảng có
/// <c>status</c> (<c>posts</c>, <c>users</c>, <c>reports</c>) xoá bằng <c>status</c>
/// và không có query filter, vì điều kiện hiển thị phụ thuộc người xem (BR-07).
/// </remarks>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
