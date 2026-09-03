namespace SocialMedia.Api.Common.Http;

/// <summary>Tên CORS policy — nguồn duy nhất cho cả khai báo lẫn nơi dùng.</summary>
public static class CorsPolicies
{
    /// <summary>Policy cho <c>socialmedia_web</c>; bật credentials vì refresh token là cookie httpOnly.</summary>
    public const string Web = "web";
}
