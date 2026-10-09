using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.Api;

internal static class ApiCors
{
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            var origins = configuration.GetSection($"{AuthenticationOptions.SectionName}:AllowedWebOrigins")
                .Get<string[]>() ?? [];
            options.AddDefaultPolicy(policy =>
            {
                if (origins.Length > 0)
                    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
                        .WithExposedHeaders("ETag", "Location", "Retry-After");
            });
        });
        return services;
    }
}
