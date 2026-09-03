using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SocialMedia.Api.Tests.Infrastructure;

/// <summary>
/// Phát access token cho test. Phase 0 chưa có module Auth nên helper này ký trực tiếp
/// bằng cùng secret mà <see cref="ApiFactory"/> nạp vào app; từ Phase 1.3 helper "đăng nhập"
/// sẽ gọi <c>POST /auth/login</c> và dùng lại chỗ này cho các kịch bản token hỏng.
/// </summary>
public static class TestJwt
{
    public const string Issuer = "socialmedia-api-tests";
    public const string Audience = "socialmedia-web-tests";
    public const string SecretKey = "test-only-secret-key-at-least-32-bytes-long!!";

    /// <summary>Token hợp lệ với claims <c>sub</c>/<c>role</c>/<c>jti</c> như SPEC mục 4.</summary>
    public static string CreateAccessToken(
        Guid? userId = null,
        string role = "User",
        TimeSpan? lifetime = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15)),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", (userId ?? Guid.NewGuid()).ToString()),
                new Claim("role", role),
                new Claim("jti", Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Token ký bằng khóa khác — dùng cho TC-A02 (chữ ký sai).</summary>
    public static string CreateTokenSignedWithWrongKey()
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(15),
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-secret-key-at-least-32-bytes-long!")),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
