using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SocialMedia.Api.Common.Auth;

/// <summary>
/// Nối <see cref="JwtOptions"/> (đã validate lúc khởi động) vào JwtBearer để chỉ có
/// một nơi mô tả cách token được ký và kiểm.
/// </summary>
public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        // Giữ nguyên tên claim `sub`/`role` thay vì để handler ánh xạ sang URI của WS-Federation.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtClaimNames.Subject,
            RoleClaimType = JwtClaimNames.Role,
        };

        options.Events = new JwtBearerEvents
        {
            // SignalR gửi token qua query string lúc handshake (ARCHITECTURE.md mục 3).
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    }
}

/// <summary>Tên claim trong access token — nguồn duy nhất cho cả bên phát lẫn bên kiểm.</summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string TokenId = "jti";
}
