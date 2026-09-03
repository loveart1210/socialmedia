namespace SocialMedia.Api.Common.Http;

/// <summary>
/// Mẫu route dùng chung — nguồn duy nhất cho prefix <c>/api</c> + URI versioning
/// (ARCHITECTURE.md mục 3). Controller nghiệp vụ luôn khai
/// <c>[Route(ApiRoutes.VersionedController)]</c>, không tự viết chuỗi route.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Prefix chung của toàn bộ API.</summary>
    public const string Prefix = "api";

    /// <summary>Route chuẩn của controller nghiệp vụ: <c>/api/v1/&lt;resource&gt;</c>.</summary>
    public const string VersionedController = Prefix + "/v{version:apiVersion}/[controller]";
}
