using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Notifications.Application.Delivery;
using Zpi.Modules.Notifications.Application.Inbox;
using Zpi.Modules.Notifications.Application.Outbox;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Routines.Application.Reminders;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class NotificationTests(IdentityApiFixture fixture)
{
    private const string Password = "Notification123!";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [PostgresFact]
    public async Task Routine_reminders_are_queued_atomically_and_sent_once_per_term_with_safe_payloads()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        var occurrence = await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddMinutes(15));
        await Process(factory);
        await Process(factory);
        var inbox = await Inbox(senior);
        var first = Assert.Single(inbox.Items);
        Assert.Equal(occurrence, first.Target.OccurrenceId);
        Assert.DoesNotContain("Secret medication", first.Message);
        Assert.Equal("inApp", Assert.Single(first.Deliveries).Channel);
        Assert.Equal("delivered", Assert.Single(first.Deliveries).Status);
        Assert.Empty(factory.Services.GetRequiredService<RecordingSender>().Sms);
        clock.Advance(TimeSpan.FromMinutes(15));
        await Process(factory);
        await Process(factory);
        var later = await Inbox(senior);
        Assert.Equal(2, later.Items.Count);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        var events = await db.OutboxMessages.Where(row => EF.Functions.JsonContains(row.Payload, JsonSerializer.Serialize(new { RecipientAccountId = senior.Id }))).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, row => Assert.NotNull(row.ProcessedAt));
    }

    [PostgresFact]
    public async Task Cancelled_or_decided_occurrences_do_not_send_queued_reminders()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        var id = await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddMinutes(15));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RoutineReminderSource>().PrepareAsync(CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
            var row = (await db.TaskOccurrences.FindAsync(id))!;
            row.Status = OccurrenceStatus.Cancelled;
            await db.SaveChangesAsync();
        }
        await Process(factory);
        Assert.Empty((await Inbox(senior)).Items);
    }

    [PostgresFact]
    public async Task Inbox_is_private_paginated_and_read_is_idempotent()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var other = await Actor(factory, AccountRole.Senior);
        await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddMinutes(15));
        await Process(factory);
        var notification = Assert.Single((await Inbox(senior)).Items);
        await Error(await other.Client.GetAsync("/api/v1/me/notifications/" + notification.NotificationId), 404, "resourceNotFound");
        await Error(await other.Client.PutAsync("/api/v1/me/notifications/" + notification.NotificationId + "/read", null), 404, "resourceNotFound");
        var read = await senior.Client.PutAsync("/api/v1/me/notifications/" + notification.NotificationId + "/read", null);
        await Status(read, 200);
        var at = (await read.Content.ReadFromJsonAsync<NotificationDto>())!.ReadAt;
        clock.Advance(TimeSpan.FromSeconds(30));
        var repeated = await senior.Client.PutAsync("/api/v1/me/notifications/" + notification.NotificationId + "/read", null);
        await Status(repeated, 200);
        Assert.Equal(at, (await repeated.Content.ReadFromJsonAsync<NotificationDto>())!.ReadAt);
        Assert.Empty((await Inbox(senior, "isRead=false")).Items);
        Assert.Single((await Inbox(senior, "isRead=true&type=routineReminder&limit=1")).Items);
        Assert.Null((await Inbox(senior, "limit=1")).NextCursor);
        await Error(await senior.Client.GetAsync("/api/v1/me/notifications?limit=0"), 400, "validationFailed");
        await Error(await senior.Client.GetAsync("/api/v1/me/notifications?from=2026-10-15T00:00:00Z&to=2026-10-14T00:00:00Z"), 400, "validationFailed");
        var raw = await senior.Client.GetAsync("/api/v1/me/notifications/" + notification.NotificationId);
        Assert.True(raw.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain("TokenHash", await raw.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task Inbox_combines_senior_filter_with_inclusive_start_and_exclusive_end()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var other = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Link(other, caretaker);
        var start = clock.GetUtcNow();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsFacade>();
        async Task Enqueue(Guid seniorId, string title) => await notifications.EnqueueAsync("test",
            new NotificationEvent(Guid.CreateVersion7(), caretaker.Id, seniorId, NotificationType.LowBattery,
                title, "Podłącz ładowarkę.", NotificationTargetKind.Incident, Guid.CreateVersion7(), null),
            CancellationToken.None);
        await Enqueue(senior.Id, "Na początku");
        await Enqueue(other.Id, "Inny podopieczny");
        clock.Advance(TimeSpan.FromMinutes(1));
        var end = clock.GetUtcNow();
        await Enqueue(senior.Id, "Na końcu");

        var from = Uri.EscapeDataString(start.ToOffset(TimeSpan.FromHours(1)).ToString("O"));
        var to = Uri.EscapeDataString(end.ToString("O"));
        var bounded = await Inbox(caretaker, $"seniorId={senior.Id}&from={from}&to={to}");
        Assert.Equal("Na początku", Assert.Single(bounded.Items).Title);
        Assert.Equal(2, (await Inbox(caretaker, $"seniorId={senior.Id}&from={from}")).Items.Count);
        Assert.Equal(2, (await Inbox(caretaker, $"to={to}")).Items.Count);
    }

    [PostgresFact]
    public async Task Caretaker_sms_and_history_require_current_care_access()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var id = Guid.CreateVersion7();
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<INotificationsFacade>().EnqueueAsync("test", new NotificationEvent(id, caretaker.Id,
                senior.Id, NotificationType.Sos, "Alarm", "Senior potrzebuje pomocy. Otwórz aplikację.", NotificationTargetKind.Incident,
                Guid.CreateVersion7(), null), CancellationToken.None);
        await Process(factory);
        var item = Assert.Single((await Inbox(caretaker)).Items);
        Assert.Contains(item.Deliveries, row => row.Channel == "sms" && row.Status == "sent");
        Assert.Single(factory.Services.GetRequiredService<RecordingSender>().Sms);
        await Status(await caretaker.Client.DeleteAsync("/api/v1/care-relationships/" + relationship.RelationshipId), 204);
        Assert.Empty((await Inbox(caretaker)).Items);
        await Error(await caretaker.Client.GetAsync("/api/v1/me/notifications/" + item.NotificationId), 404, "resourceNotFound");
    }

    [PostgresFact]
    public async Task Removed_relationship_cancels_unsent_sms()
    {
        await using var factory = Factory(new TestClock());
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<INotificationsFacade>().EnqueueAsync("test", new NotificationEvent(Guid.CreateVersion7(), caretaker.Id,
                senior.Id, NotificationType.Sos, "Alarm", "Senior potrzebuje pomocy.", NotificationTargetKind.Incident, Guid.CreateVersion7(), null), CancellationToken.None);
        await Status(await caretaker.Client.DeleteAsync("/api/v1/care-relationships/" + relationship.RelationshipId), 204);
        await Process(factory);
        Assert.Empty(factory.Services.GetRequiredService<RecordingSender>().Sms);
        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var ids = db.Notifications.Where(row => row.RecipientAccountId == caretaker.Id).Select(row => row.Id);
        Assert.All(await db.NotificationDeliveries.Where(row => ids.Contains(row.NotificationId) && row.Channel == DeliveryChannel.Sms).ToListAsync(), row => Assert.Equal(DeliveryStatus.Cancelled, row.Status));
    }

    [PostgresFact]
    public async Task Low_battery_reaches_caretakers_in_app_without_an_sms()
    {
        await using var factory = Factory(new TestClock());
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<INotificationsFacade>().EnqueueAsync("test", new NotificationEvent(Guid.CreateVersion7(), caretaker.Id,
                senior.Id, NotificationType.LowBattery, "Niski poziom baterii", "Urządzenie seniora ma niski poziom baterii.",
                NotificationTargetKind.Incident, Guid.CreateVersion7(), null), CancellationToken.None);
        var item = Assert.Single((await Inbox(caretaker)).Items);
        Assert.Equal("inApp", Assert.Single(item.Deliveries).Channel);
    }

    [PostgresFact]
    public async Task Duplicate_events_and_parallel_processors_do_not_create_duplicate_notifications_or_claims()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        var source = factory.Services.GetRequiredService<TestSource>();
        source.Event = new NotificationEvent(Guid.CreateVersion7(), senior.Id, senior.Id, NotificationType.LowBattery, "Bateria", "Podłącz ładowarkę.", NotificationTargetKind.Incident, Guid.CreateVersion7(), null);
        source.FailCompletion = true;
        await Process(factory);
        await Task.WhenAll(Process(factory), Process(factory));
        Assert.Single((await Inbox(senior)).Items);
        Assert.True(source.Completed);
    }

    [PostgresFact]
    public async Task Expired_reminders_and_deactivated_accounts_are_not_sent_and_cleanup_removes_outbox()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddMinutes(15));
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RoutineReminderSource>().PrepareAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(16));
        await using (var account = fixture.Context())
        {
            var row = (await account.Accounts.FindAsync(senior.Id))!;
            row.Status = AccountStatus.PendingDeletion;
            await account.SaveChangesAsync();
        }
        await Process(factory);
        await using var check = factory.Services.CreateAsyncScope();
        var ownIds = await check.ServiceProvider.GetRequiredService<NotificationsDbContext>().Notifications
            .Where(row => row.RecipientAccountId == senior.Id).Select(row => row.Id).ToArrayAsync();
        await using var cleanup = factory.Services.CreateAsyncScope();
        foreach (var service in cleanup.ServiceProvider.GetServices<IAccountDataCleanup>()) await service.DeleteAsync(senior.Id, CancellationToken.None);
        var db = cleanup.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var json = JsonSerializer.Serialize(new { RecipientAccountId = senior.Id });
        Assert.False(await db.OutboxMessages.AnyAsync(row => EF.Functions.JsonContains(row.Payload, json)));
    }

    [PostgresFact]
    public async Task Notification_cursors_traverse_equal_timestamps_without_duplicates()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        for (var index = 0; index < 3; index++)
            await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddMinutes(15));
        await Process(factory);
        var expected = await Inbox(senior);
        Assert.Equal(3, expected.Items.Count);
        var ids = new List<Guid>();
        string? cursor = null;
        do
        {
            var portion = await Inbox(senior, "limit=1" + (cursor is null ? "" : "&cursor=" + cursor));
            ids.Add(Assert.Single(portion.Items).NotificationId);
            cursor = portion.NextCursor;
            Assert.InRange(ids.Count, 1, 3);
        } while (cursor is not null);
        Assert.Equal(expected.Items.Select(row => row.NotificationId), ids);
    }

    [PostgresFact]
    public async Task Anonymous_requests_cannot_use_notifications()
    {
        await using var factory = Factory(new TestClock());
        using var client = factory.CreateClient();
        var id = Guid.CreateVersion7();
        await Error(await client.GetAsync("/api/v1/me/notifications"), 401, "unauthenticated");
        await Error(await client.GetAsync("/api/v1/me/notifications/" + id), 401, "unauthenticated");
        await Error(await client.PutAsync("/api/v1/me/notifications/" + id + "/read", null), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Malformed_outbox_records_are_retained_for_retry_without_blocking_valid_records()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        var id = Guid.CreateVersion7();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
            db.OutboxMessages.Add(new Zpi.Database.Outbox.OutboxMessage
            {
                Id = id,
                Type = RoutineReminderEvent.Type,
                ContractVersion = 99,
                Payload = "{}",
                OccurredAt = clock.GetUtcNow()
            });
            await db.SaveChangesAsync();
        }
        await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddMinutes(15));
        await Process(factory);
        Assert.Single((await Inbox(senior)).Items);
        await using var check = factory.Services.CreateAsyncScope();
        var row = (await check.ServiceProvider.GetRequiredService<RoutinesDbContext>().OutboxMessages.FindAsync(id))!;
        Assert.Null(row.ProcessedAt);
        Assert.Equal(1, row.Attempts);
        Assert.Equal("notificationProcessingFailed", row.LastError);
        Assert.NotNull(row.NextAttemptAt);
    }

    [PostgresFact]
    public async Task Outbox_isolates_source_failures_and_retries_missing_and_mismatched_payloads()
    {
        await using var factory = Factory(new TestClock());
        using var senior = await Actor(factory, AccountRole.Senior);
        var eventId = Guid.CreateVersion7();
        var message = new NotificationEvent(eventId, senior.Id, senior.Id, NotificationType.LowBattery,
            "Bateria", "Podłącz ładowarkę.", NotificationTargetKind.Incident, Guid.CreateVersion7(), null);
        var missing = new PendingNotificationEvent(Guid.CreateVersion7(), 1, "null");
        var mismatched = new PendingNotificationEvent(Guid.CreateVersion7(), 1, JsonSerializer.Serialize(message));
        var source = new TestSource
        {
            Pending = [missing, mismatched, new(eventId, 1, JsonSerializer.Serialize(message))]
        };
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<NotificationOutboxProcessor>();
        await processor.ProcessAsync([new TestSource { FailPreparation = true }, source], CancellationToken.None);

        Assert.Equal(new[] { missing.Id, mismatched.Id }, source.Retried);
        Assert.True(source.Completed);
        Assert.Equal("Bateria", Assert.Single((await Inbox(senior)).Items).Title);
    }

    [PostgresFact]
    public async Task Outbox_does_not_retry_events_when_processing_is_cancelled()
    {
        await using var factory = Factory(new TestClock());
        using var cancellation = new CancellationTokenSource();
        var id = Guid.CreateVersion7();
        var source = new TestSource
        {
            Pending =
            [
                new(id, 1, JsonSerializer.Serialize(new NotificationEvent(id, Guid.CreateVersion7(), null,
                    NotificationType.LowBattery, "Bateria", "Podłącz ładowarkę.", NotificationTargetKind.Incident,
                    Guid.CreateVersion7(), null)))
            ],
            OnRelevanceCheck = token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }
        };
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<NotificationOutboxProcessor>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync([source], cancellation.Token));
        Assert.False(source.Completed);
        Assert.Empty(source.Retried);
    }

    [PostgresFact]
    public async Task Care_invitation_notification_targets_the_invitation_without_a_senior_filter()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var invitationId = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsFacade>();
        var message = new NotificationEvent(Guid.CreateVersion7(), caretaker.Id, null, NotificationType.CareInvitation,
            "Zaproszenie", "Otwórz zaproszenie.", NotificationTargetKind.CareInvitation, invitationId, null);
        await notifications.EnqueueAsync("test", message with { ExpiresAt = clock.GetUtcNow() }, CancellationToken.None);
        await notifications.EnqueueAsync("test", message with { RecipientAccountId = Guid.CreateVersion7() }, CancellationToken.None);
        Assert.Empty((await Inbox(caretaker)).Items);
        await notifications.EnqueueAsync("test", message, CancellationToken.None);

        var item = Assert.Single((await Inbox(caretaker)).Items);
        Assert.Null(item.SeniorId);
        Assert.Equal(invitationId, item.Target.InvitationId);
        Assert.Equal("inApp", Assert.Single(item.Deliveries).Channel);
    }

    [PostgresFact]
    public async Task Routine_reminder_rejects_unrelated_targets_recipients_and_expired_events()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        var id = await SeedOccurrence(factory, senior.Id, clock.GetUtcNow());
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<RoutineReminderSource>();
        var reminder = new NotificationEvent(Guid.CreateVersion7(), senior.Id, senior.Id, NotificationType.RoutineReminder,
            "Przypomnienie", "Otwórz aplikację.", NotificationTargetKind.TaskOccurrence, id, clock.GetUtcNow().AddMinutes(5));
        Assert.True(await source.IsRelevantAsync(reminder, CancellationToken.None));
        Assert.False(await source.IsRelevantAsync(reminder with { Type = NotificationType.Sos }, CancellationToken.None));
        Assert.False(await source.IsRelevantAsync(reminder with { TargetKind = NotificationTargetKind.Incident }, CancellationToken.None));
        Assert.False(await source.IsRelevantAsync(reminder with { RecipientAccountId = Guid.CreateVersion7() }, CancellationToken.None));
        Assert.False(await source.IsRelevantAsync(reminder with { ExpiresAt = clock.GetUtcNow() }, CancellationToken.None));
        Assert.False(await source.IsRelevantAsync(reminder with { TargetId = Guid.CreateVersion7() }, CancellationToken.None));
    }

    [PostgresFact]
    public async Task Inactive_task_does_not_send_a_recent_past_occurrence_even_when_history_remains_pending()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        var id = await SeedOccurrence(factory, senior.Id, clock.GetUtcNow().AddSeconds(-30));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
            var occurrence = (await db.TaskOccurrences.FindAsync(id))!;
            var task = (await db.RoutineTasks.FindAsync(occurrence.RoutineTaskId))!;
            task.IsActive = false;
            await db.SaveChangesAsync();
        }
        await Process(factory);
        Assert.Empty((await Inbox(senior)).Items);
    }

    [PostgresFact]
    public async Task Parallel_read_operations_preserve_the_first_read_timestamp()
    {
        await using var factory = Factory(new TestClock());
        using var senior = await Actor(factory, AccountRole.Senior);
        await SeedOccurrence(factory, senior.Id, DateTimeOffset.UtcNow.AddMinutes(14));
        await Process(factory);
        var item = Assert.Single((await Inbox(senior)).Items);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => senior.Client.PutAsync(
            "/api/v1/me/notifications/" + item.NotificationId + "/read", null)));
        var times = new List<DateTimeOffset?>();
        foreach (var response in responses)
        {
            await Status(response, 200);
            times.Add((await response.Content.ReadFromJsonAsync<NotificationDto>())!.ReadAt);
        }
        Assert.NotNull(times[0]);
        Assert.Single(times.Distinct());
    }

    [PostgresFact]
    public async Task Inactive_recipients_do_not_block_following_reminder_batches()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var inactive = await Actor(factory, AccountRole.Senior);
        using var active = await Actor(factory, AccountRole.Senior);
        for (var i = 0; i < 100; i++) await SeedOccurrence(factory, inactive.Id, clock.GetUtcNow().AddMinutes(10));
        await SeedOccurrence(factory, active.Id, clock.GetUtcNow().AddMinutes(14));
        await using (var context = fixture.Context())
        {
            var account = (await context.Accounts.FindAsync(inactive.Id))!;
            account.Status = AccountStatus.PendingDeletion;
            await context.SaveChangesAsync();
        }
        await Process(factory);
        await Process(factory);
        Assert.Single((await Inbox(active)).Items);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var payload = JsonSerializer.Serialize(new { RecipientAccountId = inactive.Id });
        Assert.False(await db.OutboxMessages.AnyAsync(row => EF.Functions.JsonContains(row.Payload, payload)));
    }

    [PostgresFact]
    public async Task Sms_retries_stop_after_five_attempts_with_a_stable_key()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<INotificationsFacade>().EnqueueAsync("test",
                new NotificationEvent(Guid.CreateVersion7(), caretaker.Id, senior.Id, NotificationType.Sos,
                    "Alarm", "Otwórz aplikację.", NotificationTargetKind.Incident, Guid.CreateVersion7(), null), CancellationToken.None);
        var sender = factory.Services.GetRequiredService<RecordingSender>();
        sender.Failures = 100;
        foreach (var delay in new[] { 0, 16, 31, 61, 121 })
        {
            clock.Advance(TimeSpan.FromSeconds(delay));
            await Process(factory);
        }
        var item = Assert.Single((await Inbox(caretaker)).Items);
        Assert.Contains(item.Deliveries, row => row.Channel == "sms" && row.Status == "failed");
        Assert.Equal(5, sender.Sms.Count);
        Assert.Single(sender.Sms.Distinct());
        clock.Advance(TimeSpan.FromMinutes(5));
        await Process(factory);
        Assert.Equal(5, sender.Sms.Count);
    }

    [PostgresFact]
    public async Task Sms_provider_exception_is_retried_without_creating_a_second_notification()
    {
        var clock = new TestClock();
        await using var factory = Factory(clock);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<INotificationsFacade>().EnqueueAsync("test",
                new NotificationEvent(Guid.CreateVersion7(), caretaker.Id, senior.Id, NotificationType.Sos,
                    "Alarm", "Otwórz aplikację.", NotificationTargetKind.Incident, Guid.CreateVersion7(), null), CancellationToken.None);
        var sender = factory.Services.GetRequiredService<RecordingSender>();
        sender.ThrowOnce = true;

        await Process(factory);
        var initial = Assert.Single((await Inbox(caretaker)).Items);
        Assert.Contains(initial.Deliveries, row => row.Channel == "sms" && row.Status == "pending");
        await Process(factory);
        Assert.Single(sender.Sms);

        clock.Advance(TimeSpan.FromSeconds(15));
        await Process(factory);
        var retried = Assert.Single((await Inbox(caretaker)).Items);
        Assert.Equal(initial.NotificationId, retried.NotificationId);
        Assert.Contains(retried.Deliveries, row => row.Channel == "sms" && row.Status == "sent");
        Assert.Equal(2, sender.Sms.Count);
        Assert.Single(sender.Sms.Distinct());
    }

    private WebApplicationFactory<Program> Factory(TestClock clock) => fixture.Factory().WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<RecordingSender>();
            services.AddSingleton<ISmsNotificationSender>(provider => provider.GetRequiredService<RecordingSender>());
            services.AddSingleton<TestSource>();
            services.AddSingleton<INotificationEventSource>(provider => provider.GetRequiredService<TestSource>());
            services.AddScoped<RoutineReminderSource>();
        }));
    private static async Task Process(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationProcessor>().ProcessAsync(CancellationToken.None);
    }
    private static async Task<CursorPageDto<NotificationDto>> Inbox(TestActor actor, string? query = null) =>
        (await actor.Client.GetFromJsonAsync<CursorPageDto<NotificationDto>>("/api/v1/me/notifications" + (query is null ? "" : "?" + query)))!;
    private static async Task<Guid> SeedOccurrence(WebApplicationFactory<Program> factory, Guid seniorId, DateTimeOffset scheduledAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoutinesDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var task = new RoutineTask
        {
            SeniorId = seniorId,
            Type = RoutineType.Medication,
            Title = "Secret medication",
            Instructions = "Secret dose",
            StartsOn = DateOnly.FromDateTime(now.UtcDateTime),
            IsActive = true,
            ScheduleRevision = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByAccountId = seniorId,
            UpdatedByAccountId = seniorId
        };
        var occurrence = new TaskOccurrence
        {
            SeniorId = seniorId,
            RoutineTaskId = task.Id,
            ScheduleRevision = 1,
            LocalDate = DateOnly.FromDateTime(scheduledAt.UtcDateTime),
            LocalTime = TimeOnly.FromDateTime(scheduledAt.UtcDateTime),
            ScheduledAt = scheduledAt,
            TimeZone = "UTC",
            Type = task.Type,
            Title = task.Title,
            Instructions = task.Instructions,
            Status = OccurrenceStatus.Pending,
            UpdatedAt = now
        };
        db.RoutineTasks.Add(task);
        db.TaskOccurrences.Add(occurrence);
        await db.SaveChangesAsync();
        return occurrence.Id;
    }
    private sealed class RecordingSender : ISmsNotificationSender
    {
        public List<string> Sms { get; } = [];
        public int Failures { get; set; }
        public bool ThrowOnce { get; set; }
        public Task<DeliverySendResult> SendAsync(string idempotencyKey, string phoneNumber, string message, CancellationToken cancellationToken)
        {
            lock (Sms) Sms.Add(idempotencyKey);
            if (ThrowOnce)
            {
                ThrowOnce = false;
                throw new HttpRequestException("Provider disconnected.");
            }
            return Task.FromResult(Failures-- > 0
                ? new DeliverySendResult(false, ErrorCode: "temporary")
                : new DeliverySendResult(true, idempotencyKey));
        }
    }
    private sealed class TestSource : INotificationEventSource
    {
        public string Name => "test";
        public NotificationEvent? Event { get; set; }
        public bool FailCompletion { get; set; }
        public bool FailPreparation { get; init; }
        public IReadOnlyList<PendingNotificationEvent>? Pending { get; init; }
        public ConcurrentQueue<Guid> Retried { get; } = new();
        public Action<CancellationToken>? OnRelevanceCheck { get; init; }
        public bool Completed { get; private set; }
        public Task PrepareAsync(CancellationToken cancellationToken) =>
            FailPreparation ? throw new InvalidOperationException("Source unavailable.") : Task.CompletedTask;
        public Task<IReadOnlyList<PendingNotificationEvent>> ReadPendingAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PendingNotificationEvent>>(Pending ?? (Event is null || Completed ? [] : [new(Event.EventId, 1, JsonSerializer.Serialize(Event))]));
        public Task<bool> IsRelevantAsync(NotificationEvent notification, CancellationToken cancellationToken)
        {
            OnRelevanceCheck?.Invoke(cancellationToken);
            return Task.FromResult(true);
        }
        public Task CompleteAsync(Guid eventId, CancellationToken cancellationToken)
        { if (FailCompletion) { FailCompletion = false; throw new InvalidOperationException("Simulated failure after enqueue."); } Completed = true; return Task.CompletedTask; }
        public Task RetryAsync(Guid eventId, CancellationToken cancellationToken)
        {
            Retried.Enqueue(eventId);
            return Task.CompletedTask;
        }
    }
    private async Task<TestActor> Actor(WebApplicationFactory<Program> factory, AccountRole role)
    {
        var phone = "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        await using var context = fixture.Context();
        var now = DateTimeOffset.UtcNow;
        var account = new Account
        {
            RoleId = await context.Roles.Where(row => row.Code == role).Select(row => row.Id).SingleAsync(),
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            PasswordHash = "",
            FirstName = "Jan",
            LastName = "Kowalski",
            Status = AccountStatus.Active,
            PhoneVerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        account.PasswordHash = factory.Services.GetRequiredService<PasswordHashing>().Hash(account, Password);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        await Status(response, 200);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new TestActor(account.Id, phone, client);
    }
    private static async Task<CareRelationshipDto> Link(TestActor senior, TestActor caretaker)
    {
        var created = await caretaker.Client.PostAsync("/api/v1/care-invitations", null);
        await Status(created, 201);
        var invitation = (await created.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code });
        await Status(accepted, 200);
        return (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? etag = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return client.SendAsync(request);
    }
    private static async Task Status(HttpResponseMessage response, int status) =>
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private static async Task Error(HttpResponseMessage response, int status, string code)
    {
        await Status(response, status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan delta) => now += delta;
    }
    private sealed record TestActor(Guid Id, string Phone, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
