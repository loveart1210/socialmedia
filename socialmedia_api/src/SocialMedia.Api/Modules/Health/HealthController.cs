using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Api.Common.Http;
using SocialMedia.Api.Modules.Health.Dtos;

namespace SocialMedia.Api.Modules.Health;

/// <summary>
/// Healthcheck cho hạ tầng deploy. Version-neutral: đường dẫn là <c>/api/health</c>,
/// không có <c>/v1</c> (ARCHITECTURE.md mục 3).
/// </summary>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[Route(ApiRoutes.Prefix + "/health")]
public sealed class HealthController(HealthService service) : ControllerBase
{
    /// <summary>Trả 200 khi PostgreSQL và Redis đều trả lời được, 503 khi có phụ thuộc hỏng.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var result = await service.CheckAsync(cancellationToken);

        return StatusCode(
            result.IsHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            result);
    }
}
