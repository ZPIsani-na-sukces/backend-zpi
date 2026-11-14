using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class CascadeDeletionTests(IdentityApiFixture fixture)
{
    [PostgresFact]
    public async Task Account_deletion_cascades_through_tickets_and_challenges_but_preserves_other_accounts_and_roles()
    {
        await using var context = fixture.Context();
        var roleId = await context.Roles.Where(row => row.Code == AccountRole.Senior).Select(row => row.Id).SingleAsync();
        var accounts = Enumerable.Range(0, 2).Select(_ =>
        {
            var phone = "+48" + Random.Shared.NextInt64(100_000_000, 1_000_000_000);
            return new Account
            {
                RoleId = roleId, PhoneNumber = phone, NormalizedPhoneNumber = phone,
                PasswordHash = "test-hash", FirstName = "Cascade", LastName = "Test", Status = AccountStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };
        }).ToArray();
        context.Accounts.AddRange(accounts);
        var now = DateTimeOffset.UtcNow;
        var tickets = accounts.Select(account => new VerificationTicket
        {
            AccountId = account.Id, TokenHash = Guid.CreateVersion7().ToString("N"),
            CreatedAt = now, ExpiresAt = now.AddMinutes(10)
        }).ToArray();
        context.VerificationTickets.AddRange(tickets);
        var challenges = tickets.SelectMany(ticket => new[]
        {
            new VerificationChallenge
            {
                AccountId = ticket.AccountId, Destination = "+48123456789", SecretHash = "test-hash",
                CreatedAt = now, ExpiresAt = now.AddMinutes(5), ResendAfter = now
            },
            new VerificationChallenge
            {
                TicketId = ticket.Id, Destination = "+48123456789", SecretHash = "test-hash",
                CreatedAt = now, ExpiresAt = now.AddMinutes(5), ResendAfter = now
            },
            new VerificationChallenge
            {
                AccountId = ticket.AccountId, TicketId = ticket.Id, Destination = "+48123456789", SecretHash = "test-hash",
                CreatedAt = now, ExpiresAt = now.AddMinutes(5), ResendAfter = now
            }
        }).ToArray();
        context.VerificationChallenges.AddRange(challenges);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var restriction = await Assert.ThrowsAsync<PostgresException>(() => context.Roles.Where(row => row.Id == roleId).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, restriction.SqlState);
        await context.Accounts.Where(row => row.Id == accounts[0].Id).ExecuteDeleteAsync();
        Assert.False(await context.VerificationTickets.AnyAsync(row => row.Id == tickets[0].Id));
        var removed = challenges.Take(3).Select(row => row.Id).ToArray();
        var kept = challenges.Skip(3).Select(row => row.Id).ToArray();
        Assert.False(await context.VerificationChallenges.AnyAsync(row => removed.Contains(row.Id)));
        Assert.Equal(3, await context.VerificationChallenges.CountAsync(row => kept.Contains(row.Id)));
        Assert.True(await context.Accounts.AnyAsync(row => row.Id == accounts[1].Id));
        Assert.True(await context.VerificationTickets.AnyAsync(row => row.Id == tickets[1].Id));
        Assert.True(await context.Roles.AnyAsync(row => row.Id == roleId));
    }

    [PostgresFact]
    public async Task Permanent_task_deletion_removes_its_schedule_occurrences_and_decisions_while_soft_delete_preserves_history()
    {
        await using var factory = fixture.Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var now = DateTimeOffset.UtcNow;
        var tasks = Enumerable.Range(0, 2).Select(_ => new RoutineTask
        {
            SeniorId = Guid.CreateVersion7(), Title = "Medication", Instructions = "With water",
            StartsOn = DateOnly.FromDateTime(now.UtcDateTime), CreatedByAccountId = Guid.CreateVersion7(),
            CreatedAt = now, UpdatedAt = now
        }).ToArray();
        context.RoutineTasks.AddRange(tasks);
        context.RoutineTaskSchedules.AddRange(tasks.Select(task => new RoutineTaskSchedule
        {
            RoutineTaskId = task.Id, Revision = 1, DaysOfWeek = [1, 2, 3, 4, 5, 6, 7], TimeZone = "UTC"
        }));
        var occurrences = tasks.Select(task => new TaskOccurrence
        {
            RoutineTaskId = task.Id, SeniorId = task.SeniorId, ScheduleRevision = 1,
            Title = task.Title, Instructions = task.Instructions, TimeZone = "UTC", Status = OccurrenceStatus.Confirmed,
            LocalDate = task.StartsOn, ScheduledAt = now, UpdatedAt = now
        }).ToArray();
        context.TaskOccurrences.AddRange(occurrences);
        context.TaskConfirmations.AddRange(occurrences.Select(occurrence => new TaskConfirmation
        {
            TaskOccurrenceId = occurrence.Id, RecordedByAccountId = occurrence.SeniorId,
            Decision = TaskDecision.Confirmed, PerformedAt = now, ReceivedAt = now
        }));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await context.RoutineTasks.Where(row => row.Id == tasks[0].Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.DeletedAt, now));
        Assert.True(await context.TaskOccurrences.AnyAsync(row => row.Id == occurrences[0].Id));
        Assert.True(await context.TaskConfirmations.AnyAsync(row => row.TaskOccurrenceId == occurrences[0].Id));
        await context.RoutineTasks.Where(row => row.Id == tasks[0].Id).ExecuteDeleteAsync();
        Assert.False(await context.RoutineTaskSchedules.AnyAsync(row => row.RoutineTaskId == tasks[0].Id));
        Assert.False(await context.TaskOccurrences.AnyAsync(row => row.Id == occurrences[0].Id));
        Assert.False(await context.TaskConfirmations.AnyAsync(row => row.TaskOccurrenceId == occurrences[0].Id));
        Assert.True(await context.RoutineTasks.AnyAsync(row => row.Id == tasks[1].Id));
        Assert.True(await context.RoutineTaskSchedules.AnyAsync(row => row.RoutineTaskId == tasks[1].Id));
        Assert.True(await context.TaskOccurrences.AnyAsync(row => row.Id == occurrences[1].Id));
        Assert.True(await context.TaskConfirmations.AnyAsync(row => row.TaskOccurrenceId == occurrences[1].Id));
    }

    [PostgresFact]
    public async Task Notification_deletion_removes_all_its_deliveries_without_touching_another_recipient()
    {
        await using var factory = fixture.Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var notifications = Enumerable.Range(0, 2).Select(_ => new Notification
        {
            RecipientAccountId = Guid.CreateVersion7(), IntegrationEventId = Guid.CreateVersion7(),
            Title = "SOS", Message = "Please check the app", Type = NotificationType.Sos,
            TargetKind = NotificationTargetKind.Incident, TargetId = Guid.CreateVersion7(), CreatedAt = DateTimeOffset.UtcNow
        }).ToArray();
        context.Notifications.AddRange(notifications);
        context.NotificationDeliveries.AddRange(notifications.SelectMany(notification => new[] { DeliveryChannel.InApp, DeliveryChannel.Sms }
            .Select(channel => new NotificationDelivery { NotificationId = notification.Id, Channel = channel, Status = DeliveryStatus.Pending })));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await context.Notifications.Where(row => row.Id == notifications[0].Id).ExecuteDeleteAsync();
        Assert.False(await context.NotificationDeliveries.AnyAsync(row => row.NotificationId == notifications[0].Id));
        Assert.True(await context.Notifications.AnyAsync(row => row.Id == notifications[1].Id));
        Assert.Equal(2, await context.NotificationDeliveries.CountAsync(row => row.NotificationId == notifications[1].Id));
    }

    [PostgresFact]
    public async Task Device_deletion_cascades_telemetry_and_detector_state_but_cannot_erase_incident_history()
    {
        await using var factory = fixture.Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        var now = DateTimeOffset.UtcNow;
        var devices = Enumerable.Range(0, 2).Select(_ => new Device
        {
            AssignedAccountId = Guid.CreateVersion7(), InstallationId = Guid.CreateVersion7(),
            AppVersion = "1.0", RegisteredAt = now, Platform = DevicePlatform.Android
        }).ToArray();
        context.Devices.AddRange(devices);
        context.LocationSamples.AddRange(devices.Select(device => new LocationSample
        {
            DeviceId = device.Id, Latitude = 52, Longitude = 21, CapturedAt = now, ReceivedAt = now
        }));
        context.BatterySamples.AddRange(devices.Select(device => new BatterySample
        {
            DeviceId = device.Id, BatteryLevelPercent = 50, CapturedAt = now, ReceivedAt = now
        }));
        context.AutomaticAlertStates.AddRange(devices.Select(device => new AutomaticAlertState
        {
            SeniorId = device.AssignedAccountId, DeviceId = device.Id
        }));
        var incident = new SosIncident
        {
            SeniorId = devices[0].AssignedAccountId, DeviceId = devices[0].Id, Status = IncidentStatus.Open,
            OccurredAt = now, ReceivedAt = now
        };
        context.SosIncidents.Add(incident);
        context.SosTriggerReceipts.Add(new SosTriggerReceipt { DeviceId = devices[0].Id, IncidentId = incident.Id, TriggeredAt = now });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var restriction = await Assert.ThrowsAsync<PostgresException>(() => context.Devices.Where(row => row.Id == devices[0].Id).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, restriction.SqlState);
        Assert.True(await context.SosIncidents.AnyAsync(row => row.Id == incident.Id));
        Assert.True(await context.SosTriggerReceipts.AnyAsync(row => row.IncidentId == incident.Id));
        Assert.True(await context.LocationSamples.AnyAsync(row => row.DeviceId == devices[0].Id));
        Assert.True(await context.BatterySamples.AnyAsync(row => row.DeviceId == devices[0].Id));
        Assert.True(await context.AutomaticAlertStates.AnyAsync(row => row.DeviceId == devices[0].Id));
        await context.SosIncidents.Where(row => row.Id == incident.Id).ExecuteDeleteAsync();
        Assert.False(await context.SosTriggerReceipts.AnyAsync(row => row.IncidentId == incident.Id));
        Assert.True(await context.Devices.AnyAsync(row => row.Id == devices[0].Id));
        await context.Devices.Where(row => row.Id == devices[0].Id).ExecuteDeleteAsync();
        Assert.False(await context.LocationSamples.AnyAsync(row => row.DeviceId == devices[0].Id));
        Assert.False(await context.BatterySamples.AnyAsync(row => row.DeviceId == devices[0].Id));
        Assert.False(await context.AutomaticAlertStates.AnyAsync(row => row.DeviceId == devices[0].Id));
        Assert.True(await context.LocationSamples.AnyAsync(row => row.DeviceId == devices[1].Id));
        Assert.True(await context.BatterySamples.AnyAsync(row => row.DeviceId == devices[1].Id));
        Assert.True(await context.AutomaticAlertStates.AnyAsync(row => row.DeviceId == devices[1].Id));
    }
}
