using Microsoft.AspNetCore.Builder;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.Notifications.Api;

public static class NotificationsApiSetup
{
    public static IServiceCollection AddNotificationsApi(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
        services.AddRateLimiter(options => options.AddPolicy("notifications", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }
}
