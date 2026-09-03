using System.ComponentModel.DataAnnotations;

namespace SocialMedia.Api.Common.Auth;

/// <summary>
/// Cấu hình JWT access token (SPEC mục 4): HS256, TTL 15 phút, claims <c>sub</c>/<c>role</c>/<c>jti</c>.
/// Secret lấy từ <c>appsettings.Development.json</c> ở local và biến môi trường ở prod.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Khóa ký HS256 — tối thiểu 32 byte, không hardcode trong code.</summary>
    [Required]
    [MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>TTL access token, mặc định 15 phút (SPEC mục 4).</summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>TTL refresh token (dùng từ Phase 1.3).</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}
