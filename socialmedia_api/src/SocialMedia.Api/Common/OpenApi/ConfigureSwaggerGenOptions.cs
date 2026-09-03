using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SocialMedia.Api.Common.OpenApi;

/// <summary>
/// Sinh một Swagger document cho mỗi API version mà ApiExplorer tìm được
/// (ARCHITECTURE.md mục 3) + khai báo scheme Bearer để bấm thử trên <c>/docs</c>.
/// </summary>
public sealed class ConfigureSwaggerGenOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    private const string BearerSchemeId = "Bearer";

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "SocialMedia API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "Phiên bản này đã ngừng hỗ trợ."
                    : "API của mạng xã hội SocialMedia.",
            });
        }

        options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Dán access token (không kèm tiền tố 'Bearer').",
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = [],
        });

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
}
