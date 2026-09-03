using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SocialMedia.Api.Common.Auth;
using SocialMedia.Api.Common.Errors;
using SocialMedia.Api.Common.Http;
using SocialMedia.Api.Common.OpenApi;
using SocialMedia.Api.Common.RateLimiting;
using SocialMedia.Api.Common.Validation;
using SocialMedia.Api.Infra.Database;
using SocialMedia.Api.Infra.Redis;
using SocialMedia.Api.Modules.Health;

var builder = WebApplication.CreateBuilder(args);

// ── Hạ tầng ngoài tiến trình ────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRedis(builder.Configuration);

// ── Controller + JSON ───────────────────────────────────────────────────────
// UnmappedMemberHandling.Disallow: field không khai trong DTO → 400, không bỏ qua.
builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly, includeInternalTypes: true);

// ── Versioning: mọi route nghiệp vụ là /api/v1/<resource> ───────────────────
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// ── OpenAPI (chỉ phục vụ ở Development) ─────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerGenOptions>();

// ── Lỗi: mọi response lỗi là ProblemDetails ─────────────────────────────────
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["requestId"] = context.HttpContext.GetRequestId();
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ── AuthN / AuthZ: default deny ─────────────────────────────────────────────
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();

// Fallback policy: endpoint nào không khai gì về quyền thì vẫn phải có token;
// mở public bằng [AllowAnonymous].
builder.Services
    .AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ── CORS + rate limit ───────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicies.Web, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(RequestIdMiddleware.HeaderName)
    // Refresh token là cookie httpOnly → bắt buộc credentials.
    .AllowCredentials()));

builder.Services.AddApiRateLimiting();

// ── Module nghiệp vụ ────────────────────────────────────────────────────────
builder.Services.AddHealthModule();

var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseExceptionHandler();

// Response lỗi rỗng của pipeline (401 khi thiếu token, 404, 429…) cũng thành ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "docs";
        foreach (var description in app.Services
            .GetRequiredService<IApiVersionDescriptionProvider>()
            .ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });
}

app.UseCors(CorsPolicies.Web);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Điểm neo cho <c>WebApplicationFactory&lt;Program&gt;</c> trong test tích hợp.</summary>
public partial class Program;
