namespace SocialMedia.Api.Common.Http;

/// <summary>
/// Sinh <c>X-Request-Id</c> cho mỗi request (nhận lại giá trị client gửi lên nếu có)
/// để truy vết một thao tác xuyên log của API và hub (ARCHITECTURE.md mục 2).
/// </summary>
public sealed class RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
{
    public const string HeaderName = "X-Request-Id";

    /// <summary>Khóa trong <see cref="HttpContext.Items"/> giữ request id.</summary>
    internal const string ItemKey = "RequestId";

    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ReadIncoming(context) ?? Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = requestId;
        context.Response.Headers[HeaderName] = requestId;

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = requestId }))
        {
            await next(context);
        }
    }

    private static string? ReadIncoming(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return null;
        }

        var candidate = values.ToString();
        return string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength
            ? null
            : candidate;
    }
}

public static class RequestIdExtensions
{
    /// <summary>Đọc request id của request hiện tại (rỗng nếu middleware chưa chạy).</summary>
    public static string GetRequestId(this HttpContext context)
        => context.Items.TryGetValue(RequestIdMiddleware.ItemKey, out var value) && value is string requestId
            ? requestId
            : string.Empty;
}
