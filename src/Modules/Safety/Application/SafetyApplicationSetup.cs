using Zpi.Modules.Safety.Application.Alerts;
using Zpi.Modules.Safety.Application.Home.Detect;
using Zpi.Modules.Safety.Application.Geozones.Delete;
using Zpi.Modules.Safety.Application.Geozones.Update;
using Zpi.Modules.Safety.Application.Geozones.Create;
using Zpi.Modules.Safety.Application.Geozones.List;
using Zpi.Modules.Safety.Application.Geozones.Get;
using Zpi.Modules.Safety.Application.Home.Save;
using Zpi.Modules.Safety.Application.Home.Get;
using FluentValidation;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Safety.Application.Notifications;
using Zpi.Modules.Safety.Application.Incidents;
using Zpi.Modules.Safety.Application.Incidents.Trigger;
using Zpi.Modules.Safety.Application.Incidents.Get;
using Zpi.Modules.Safety.Application.Incidents.List;
using Zpi.Modules.Safety.Application.Incidents.Acknowledge;
using Zpi.Modules.Safety.Application.Incidents.Resolve;
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
        services.AddScoped<DetectAddressHandler>();
        services.AddScoped<GetHomeAddressHandler>();
        services.AddScoped<SaveHomeAddressHandler>();
        services.AddScoped<GetGeoZonesHandler>();
        services.AddScoped<ListGeoZonesHandler>();
        services.AddScoped<CreateGeoZoneHandler>();
        services.AddScoped<UpdateGeoZoneHandler>();
        services.AddScoped<DeleteGeoZoneHandler>();
        services.AddScoped<AutomaticAlertProcessor>();
        services.AddScoped<IncidentReader>();
        services.AddScoped<IncidentMapper>();
        services.AddScoped<TriggerSosHandler>();
        services.AddScoped<GetIncidentHandler>();
        services.AddScoped<ListIncidentsHandler>();
        services.AddScoped<AcknowledgeIncidentHandler>();
        services.AddScoped<ResolveIncidentHandler>();
        services.AddScoped<INotificationEventSource, SafetyNotificationSource>();
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
