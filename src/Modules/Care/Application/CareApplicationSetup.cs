using FluentValidation;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Invitations.Cleanup;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Care.Application.Invitations.Accept;
using Zpi.Modules.Care.Application.Invitations.Create;
using Zpi.Modules.Care.Application.Invitations.Get;
using Zpi.Modules.Care.Application.Relationships.ListCaretakers;
using Zpi.Modules.Care.Application.Relationships.ListSeniors;
using Zpi.Modules.Care.Application.Relationships.Remove;

namespace Zpi.Modules.Care.Application;

public static class CareApplicationSetup
{
    public static IServiceCollection AddCareApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<InvitationCleanupProcessor>();
        services.AddScoped<GetInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<ListSeniorsHandler>();
        services.AddScoped<ListCaretakersHandler>();
        services.AddScoped<RemoveRelationshipHandler>();
        services.AddValidatorsFromAssemblyContaining<InvitationCodeRequestValidator>();
        return services;
    }
}
