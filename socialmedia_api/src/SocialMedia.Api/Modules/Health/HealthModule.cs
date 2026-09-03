namespace SocialMedia.Api.Modules.Health;

public static class HealthModule
{
    public static IServiceCollection AddHealthModule(this IServiceCollection services)
    {
        services.AddScoped<HealthService>();
        return services;
    }
}
