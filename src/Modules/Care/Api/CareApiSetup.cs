using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.Care.Api;

public static class CareApiSetup
{
    public static IServiceCollection AddCareApi(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(CareController).Assembly);
        services.AddRateLimiter(options => options.AddPolicy("care", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }
}
