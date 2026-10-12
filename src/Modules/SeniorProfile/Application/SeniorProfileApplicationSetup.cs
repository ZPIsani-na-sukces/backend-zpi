using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.SeniorProfile.Application.Accessibility;
using Zpi.Modules.SeniorProfile.Application.Accessibility.Get;
using Zpi.Modules.SeniorProfile.Application.Accessibility.Save;
using Zpi.Modules.SeniorProfile.Application.Contacts;
using Zpi.Modules.SeniorProfile.Application.Contacts.Create;
using Zpi.Modules.SeniorProfile.Application.Contacts.Get;
using Zpi.Modules.SeniorProfile.Application.Contacts.List;
using Zpi.Modules.SeniorProfile.Application.Contacts.Update;
using Zpi.Modules.SeniorProfile.Application.Contacts.Delete;
using Zpi.Modules.SeniorProfile.Application.Contacts.Settings;

namespace Zpi.Modules.SeniorProfile.Application;

public static class SeniorProfileApplicationSetup
{
    public static IServiceCollection AddSeniorProfileApplication(this IServiceCollection services)
    {
        services.AddScoped<AccessibilityPreferencesLoader>();
        services.AddScoped<GetAccessibilityPreferencesHandler>();
        services.AddScoped<SaveAccessibilityPreferencesHandler>();
        services.AddScoped<ContactDtoMapper>();
        services.AddScoped<ContactWriteAccess>();
        services.AddScoped<CreateContactHandler>();
        services.AddScoped<GetContactHandler>();
        services.AddScoped<ListContactsHandler>();
        services.AddScoped<UpdateContactHandler>();
        services.AddScoped<DeleteContactHandler>();
        services.AddScoped<ContactSettingsLoader>();
        services.AddScoped<GetContactSettingsHandler>();
        services.AddScoped<SaveContactSettingsHandler>();
        services.AddValidatorsFromAssemblyContaining<SaveAccessibilityPreferencesRequestValidator>();
        return services;
    }
}
