using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.SeniorProfile.Application.Accessibility;
using Zpi.Modules.SeniorProfile.Application.Accessibility.Get;
using Zpi.Modules.SeniorProfile.Application.Accessibility.Save;

namespace Zpi.Modules.SeniorProfile.Application;

public static class SeniorProfileApplicationSetup
{
    public static IServiceCollection AddSeniorProfileApplication(this IServiceCollection services)
    {
        services.AddScoped<AccessibilityPreferencesLoader>();
        services.AddScoped<GetAccessibilityPreferencesHandler>();
        services.AddScoped<SaveAccessibilityPreferencesHandler>();
        services.AddValidatorsFromAssemblyContaining<SaveAccessibilityPreferencesRequestValidator>();
        return services;
    }
}
