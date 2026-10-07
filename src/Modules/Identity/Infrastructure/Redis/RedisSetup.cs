using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Zpi.Modules.Identity.Application.Sessions;

namespace Zpi.Modules.Identity.Infrastructure.Redis;

public static class RedisSetup
{
    public static IServiceCollection AddIdentityRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>().Bind(configuration.GetSection("Redis")).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(
            configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Missing Redis connection string.")));
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("identity-redis", tags: ["ready"]);
        return services;
    }
}
