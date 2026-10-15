using FluentValidation;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;

namespace Zpi.Modules.Notifications.Application.Delivery;

public sealed class NotificationEventValidator : AbstractValidator<NotificationEvent>
{
    public NotificationEventValidator()
    {
        RuleFor(row => row.EventId).NotEmpty();
        RuleFor(row => row.RecipientAccountId).NotEmpty();
        RuleFor(row => row.SeniorId).NotEqual(Guid.Empty).When(row => row.SeniorId.HasValue);
        RuleFor(row => row.Type).IsInEnum();
        RuleFor(row => row.Title).NotEmpty().MaximumLength(200);
        RuleFor(row => row.Message).NotEmpty().MaximumLength(2000);
        RuleFor(row => row.TargetKind).IsInEnum();
        RuleFor(row => row.TargetId).NotEmpty();
        RuleFor(row => row).Must(row => row.Type switch
        {
            NotificationType.RoutineReminder => row.TargetKind == NotificationTargetKind.TaskOccurrence,
            NotificationType.CareInvitation => row.TargetKind == NotificationTargetKind.CareInvitation,
            _ => row.TargetKind == NotificationTargetKind.Incident
        }).WithMessage("Notification type and target must match.");
    }
}
