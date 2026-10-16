using Microsoft.Extensions.Configuration;
using Zpi.Modules.Safety.Application.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Safety.Api;
using Zpi.Modules.Safety.Application;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety;

public static class SafetyModule
{
    public static IServiceCollection AddSafetyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AlertOptions>()
            .Bind(configuration.GetSection(AlertOptions.SectionName))
            .Validate(options => options.Freshness > TimeSpan.Zero, "Safety:Alerts:Freshness must be positive.")
            .Validate(options => options.Confirmation > TimeSpan.Zero && options.Confirmation <= options.Freshness,
                "Safety:Alerts:Confirmation must be positive and no greater than Freshness.")
            .Validate(options => options.LowBatteryPercent is >= 0 and < 100 &&
                options.BatteryRearmPercent > options.LowBatteryPercent && options.BatteryRearmPercent <= 100,
                "Safety:Alerts battery thresholds must satisfy 0 <= LowBatteryPercent < BatteryRearmPercent <= 100.")
            .ValidateOnStart();
        services.AddSafetyApi();
        services.AddSafetyApplication();
        services.AddSafetyPersistence(configuration);
        return services;
    }
}
