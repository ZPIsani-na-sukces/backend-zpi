using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Care.Application.Invitations.Accept;
using Zpi.Modules.Care.Application.Invitations.Cancel;
using Zpi.Modules.Care.Application.Invitations.Create;
using Zpi.Modules.Care.Application.Invitations.Get;
using Zpi.Modules.Care.Application.Invitations.List;
using Zpi.Modules.Care.Application.Invitations.Reject;
using Zpi.Modules.Care.Application.Relationships.ListCaretakers;
using Zpi.Modules.Care.Application.Relationships.ListSeniors;
using Zpi.Modules.Care.Application.Relationships.Remove;

namespace Zpi.Modules.Care.Application;

public static class CareApplicationSetup
{
    public static IServiceCollection AddCareApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<GetInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<RejectInvitationHandler>();
        services.AddScoped<CancelInvitationHandler>();
        services.AddScoped<ListInvitationsHandler>();
        services.AddScoped<ListSeniorsHandler>();
        services.AddScoped<ListCaretakersHandler>();
        services.AddScoped<RemoveRelationshipHandler>();
        services.AddValidatorsFromAssemblyContaining<CreateInvitationRequestValidator>();
        return services;
    }
}
