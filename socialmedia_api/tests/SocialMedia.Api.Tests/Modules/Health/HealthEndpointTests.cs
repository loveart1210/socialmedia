using System.Net;
using System.Net.Http.Json;
using SocialMedia.Api.Tests.Infrastructure;

namespace SocialMedia.Api.Tests.Modules.Health;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_khong_can_token_va_tra_200_khi_ha_tang_len()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
        Assert.Equal("healthy", body.Checks["postgres"].Status);
        Assert.Equal("healthy", body.Checks["redis"].Status);
    }

    [Fact]
    public async Task Health_la_version_neutral_nen_khong_co_duong_dan_v1()
    {
        var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Moi_response_deu_kem_header_request_id()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    private sealed record HealthPayload(string Status, Dictionary<string, HealthCheckPayload> Checks);

    private sealed record HealthCheckPayload(string Status, string? Error);
}
