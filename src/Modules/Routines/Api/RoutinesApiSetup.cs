using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.Routines.Api;

public static class RoutinesApiSetup
{
    public static IServiceCollection AddRoutinesApi(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(RoutineTasksController).Assembly);
        services.AddRateLimiter(options => options.AddPolicy("routines", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }
}
