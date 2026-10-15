using FluentValidation;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Routines.Application.Reminders;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Routines.Application.Occurrences;
using Zpi.Modules.Routines.Application.Occurrences.Decision;
using Zpi.Modules.Routines.Application.Occurrences.Get;
using Zpi.Modules.Routines.Application.Occurrences.List;
using Zpi.Modules.Routines.Application.Scheduling;
using Zpi.Modules.Routines.Application.Tasks;
using Zpi.Modules.Routines.Application.Tasks.Create;
using Zpi.Modules.Routines.Application.Tasks.Delete;
using Zpi.Modules.Routines.Application.Tasks.Get;
using Zpi.Modules.Routines.Application.Tasks.List;
using Zpi.Modules.Routines.Application.Tasks.Update;

namespace Zpi.Modules.Routines.Application;

public static class RoutinesApplicationSetup
{
    public static IServiceCollection AddRoutinesApplication(this IServiceCollection services)
    {
        services.AddScoped<INotificationEventSource, RoutineReminderSource>();
        services.AddScoped<RoutineTaskMapper>();
        services.AddScoped<TaskOccurrenceMapper>();
        services.AddScoped<OccurrenceGenerator>();
        services.AddScoped<OccurrenceProcessor>();
        services.AddScoped<CreateRoutineTaskHandler>();
        services.AddScoped<GetRoutineTaskHandler>();
        services.AddScoped<ListRoutineTasksHandler>();
        services.AddScoped<UpdateRoutineTaskHandler>();
        services.AddScoped<DeleteRoutineTaskHandler>();
        services.AddScoped<GetOccurrenceHandler>();
        services.AddScoped<ListOccurrencesHandler>();
        services.AddScoped<RecordTaskDecisionHandler>();
        services.AddValidatorsFromAssemblyContaining<SaveRoutineTaskRequestValidator>();
        return services;
    }
}
