using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.Care.Api;

public static class CareApiSetup
{
    public const string InvitationCodePolicy = "care-invitation-code";

    public static IServiceCollection AddCareApi(this IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(CareController).Assembly);
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("care", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy(InvitationCodePolicy, context => RateLimitPartition.GetSlidingWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => InvitationCodeWindow(10)));

            var ipLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsInvitationCodeRequest(context)
                    ? RateLimitPartition.GetSlidingWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => InvitationCodeWindow(50))
                    : RateLimitPartition.GetNoLimiter<string>("other-endpoints"));
            options.GlobalLimiter = options.GlobalLimiter is { } existing
                ? PartitionedRateLimiter.CreateChained(existing, ipLimiter)
                : ipLimiter;

            var previousRejection = options.OnRejected;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (!IsInvitationCodeRequest(context.HttpContext))
                {
                    if (previousRejection is not null) await previousRejection(context, cancellationToken);
                    return;
                }
                var delay = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 900;
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = delay.ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.Headers.CacheControl = "no-store";
            };
        });
        return services;
    }

    private static bool IsInvitationCodeRequest(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == InvitationCodePolicy;

    private static SlidingWindowRateLimiterOptions InvitationCodeWindow(int limit) => new()
    {
        PermitLimit = limit,
        Window = TimeSpan.FromMinutes(15),
        SegmentsPerWindow = 15,
        QueueLimit = 0,
        AutoReplenishment = true
    };
}
