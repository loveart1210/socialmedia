using System.Text.Json.Serialization;

namespace SocialMedia.Api.Modules.Health.Dtos;

/// <summary>Kết quả kiểm tra sức khỏe của API và các phụ thuộc ngoài tiến trình.</summary>
/// <param name="Status">"healthy" khi mọi phụ thuộc trả lời được, ngược lại "unhealthy".</param>
/// <param name="Checks">Trạng thái từng phụ thuộc, khóa là tên dịch vụ.</param>
public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, HealthCheckResult> Checks)
{
    public const string Healthy = "healthy";
    public const string Unhealthy = "unhealthy";

    /// <summary>API có phục vụ được không — quyết định 200 hay 503. Không trả ra body.</summary>
    [JsonIgnore]
    public bool IsHealthy => Status == Healthy;
}

/// <summary>Trạng thái một phụ thuộc.</summary>
/// <param name="Status">"healthy" hoặc "unhealthy".</param>
/// <param name="Error">Lý do lỗi, null khi khỏe.</param>
public sealed record HealthCheckResult(string Status, string? Error);
