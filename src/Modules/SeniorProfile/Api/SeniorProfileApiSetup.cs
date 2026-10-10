using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.SeniorProfile.Api;

public static class SeniorProfileApiSetup
{
    public static IServiceCollection AddSeniorProfileApi(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(AccessibilityPreferencesController).Assembly);
        services.AddRateLimiter(options => options.AddPolicy("seniorProfile", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }
}
