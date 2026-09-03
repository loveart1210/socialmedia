using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SocialMedia.Api.Common.RateLimiting;

/// <summary>
/// Rate limit cho hai nhóm endpoint mà SPEC mục 6 yêu cầu: nhóm auth (đăng nhập,
/// đăng ký, quên mật khẩu) và nhóm tạo nội dung (đăng bài, bình luận).
/// Controller gắn bằng <c>[EnableRateLimiting(RateLimitPolicies.Auth)]</c>.
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Content = "content";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Chống dò mật khẩu: theo IP, vì lúc này chưa có danh tính người gọi.
            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            // Chống spam nội dung: theo người dùng đã đăng nhập.
            options.AddPolicy(Content, context => RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    private static string PartitionKey(HttpContext context)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(subject)
            ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
            : $"user:{subject}";
    }
}
