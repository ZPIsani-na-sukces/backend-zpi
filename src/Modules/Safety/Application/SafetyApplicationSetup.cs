using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Safety.Application.Devices.List;
using Zpi.Modules.Safety.Application.Devices.Register;
using Zpi.Modules.Safety.Application.Devices.Retire;
using Zpi.Modules.Safety.Application.Devices.Switch;
using Zpi.Modules.Safety.Application.Locations.Latest;
using Zpi.Modules.Safety.Application.Telemetry.Upload;

namespace Zpi.Modules.Safety.Application;

public static class SafetyApplicationSetup
{
    public static IServiceCollection AddSafetyApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterDeviceHandler>();
        services.AddScoped<ListDevicesHandler>();
        services.AddScoped<SwitchDeviceHandler>();
        services.AddScoped<RetireDeviceHandler>();
        services.AddScoped<LatestLocationHandler>();
        services.AddScoped<UploadTelemetryHandler>();
        services.AddValidatorsFromAssemblyContaining<RegisterDeviceRequestValidator>();
        return services;
    }
}
