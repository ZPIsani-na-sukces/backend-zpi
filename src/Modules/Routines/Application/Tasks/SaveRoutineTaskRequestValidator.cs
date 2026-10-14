using FluentValidation;
using Zpi.Modules.Routines.Application.Scheduling;

namespace Zpi.Modules.Routines.Application.Tasks;

public sealed class SaveRoutineTaskRequestValidator : AbstractValidator<SaveRoutineTaskRequest>
{
    public SaveRoutineTaskRequestValidator()
    {
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.Title).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Instructions).NotEmpty().MaximumLength(2000);
        RuleFor(request => request.StartsOn).NotEmpty();
        RuleFor(request => request.EndsOn).Must((request, end) => end is null || end >= request.StartsOn);
        RuleFor(request => request.Schedule).NotNull().SetValidator(new RoutineScheduleValidator());
    }
}

public sealed class RoutineScheduleValidator : AbstractValidator<RoutineScheduleInputDto>
{
    public RoutineScheduleValidator()
    {
        RuleFor(schedule => schedule.TimeZone).NotEmpty().MaximumLength(100).Must(LocalScheduleTime.IsValidZone);
        RuleFor(schedule => schedule.DaysOfWeek).NotEmpty().Must(days => days is null || days.Count <= 7 && days.Distinct().Count() == days.Count);
        RuleForEach(schedule => schedule.DaysOfWeek).InclusiveBetween(1, 7);
        RuleFor(schedule => schedule.Times).NotEmpty().Must(times => times is null || times.Count <= 12 && times.Distinct().Count() == times.Count);
        RuleForEach(schedule => schedule.Times).Must(time => time.Ticks % TimeSpan.TicksPerSecond == 0);
    }
}
