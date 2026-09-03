using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Common.Http;

namespace SocialMedia.Api.Tests.Infrastructure;

/// <summary>
/// Endpoint chỉ tồn tại trong test, nạp vào app qua <c>AddApplicationPart</c>.
/// Nó KHÔNG khai gì về quyền — đúng trường hợp mà fallback policy
/// <c>RequireAuthenticatedUser</c> phải chặn (TC-A01/TC-A02). Phase 0 chưa có
/// endpoint nghiệp vụ nào để kiểm điều đó.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiRoutes.Prefix + "/v{version:apiVersion}/probe")]
public sealed class ProtectedProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { subject = User.FindFirst("sub")?.Value });
}
