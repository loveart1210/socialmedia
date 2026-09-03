using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SocialMedia.Api.Tests.Infrastructure;

namespace SocialMedia.Api.Tests.Common.Auth;

/// <summary>
/// Hợp đồng phân quyền tối thiểu của SPEC mục 4. TC-A03→A08 xuất hiện dần cùng
/// module tương ứng ở các phase sau.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationDefaultsTests(ApiFactory factory)
{
    private const string ProtectedRoute = "/api/v1/probe";

    /// <summary>TC-A01 — gọi API bảo vệ không kèm JWT → 401.</summary>
    [Fact]
    public async Task TC_A01_khong_co_token_thi_401_kem_problem_details()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
    }

    /// <summary>TC-A02 — token hết hạn hoặc chữ ký sai → 401.</summary>
    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-signature")]
    public async Task TC_A02_token_khong_dung_thi_401(string kind)
    {
        var token = kind == "expired"
            ? TestJwt.CreateAccessToken(lifetime: TimeSpan.FromMinutes(-5))
            : TestJwt.CreateTokenSignedWithWrongKey();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_hop_le_thi_qua_duoc_fallback_policy()
    {
        var userId = Guid.NewGuid();
        var client = factory.CreateAuthenticatedClient(userId);

        var response = await client.GetAsync(ProtectedRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProbePayload>();
        Assert.Equal(userId.ToString(), payload?.Subject);
    }

    private sealed record ProblemPayload(int? Status, string? Title, string? RequestId);

    private sealed record ProbePayload(string? Subject);
}
