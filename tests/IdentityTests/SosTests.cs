using System.Net;
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
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Safety.Application.Devices;
using Zpi.Modules.Safety.Application.Devices.Register;
using Zpi.Modules.Safety.Application.Incidents;
using Zpi.Modules.Safety.Application.Incidents.Trigger;
using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Application.Notifications;
using Zpi.Modules.Safety.Application.Telemetry.Upload;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class SosTests(IdentityApiFixture fixture)
{
    private const string Password = "SosPassword123!";
    private const string Devices = "/api/v1/me/devices";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [PostgresFact]
    public async Task Trigger_without_gps_records_the_incident_and_an_outbox_event_per_active_caretaker()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var first = await Actor(factory, AccountRole.Caretaker);
        using var second = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, first);
        await Link(senior, second);
        var device = await Register(senior, false);
        var request = new TriggerSosRequest(device.DeviceId, Now(), null);
        var response = await Trigger(senior, senior.Id, request);
        await Status(response, 201);
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal("open", incident.Status);
        Assert.Equal("sos", incident.Type);
        Assert.Equal("sos", incident.Details.Kind);
        Assert.Null(incident.Position);
        Assert.Null(incident.AcknowledgedAt);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(incident, await senior.Client.GetFromJsonAsync<IncidentDto>(response.Headers.Location!));
        Assert.Equal(incident, await first.Client.GetFromJsonAsync<IncidentDto>(Path(senior.Id, incident.IncidentId)));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        var events = await db.OutboxMessages.Where(row => row.Type == SafetyNotificationEvents.SosTriggered).ToListAsync();
        var own = events.Select(row => JsonSerializer.Deserialize<NotificationEvent>(row.Payload)!).Where(row => row.TargetId == incident.IncidentId).ToArray();
        Assert.Equal(new[] { first.Id, second.Id }.Order(), own.Select(row => row.RecipientAccountId).Order());
        Assert.Equal(1, factory.Services.GetRequiredService<RecordingSignal>().Count);
        await Process(factory);
        await Process(factory);
        Assert.Single((await Inbox(first)).Items);
        Assert.Single((await Inbox(second)).Items);
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var notificationIds = notifications.Notifications.Where(row => row.SeniorId == senior.Id).Select(row => row.Id);
        var deliveries = await notifications.NotificationDeliveries
            .Where(row => notificationIds.Contains(row.NotificationId) && row.Channel == DeliveryChannel.Sms).ToListAsync();
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, row => Assert.Equal(DeliveryStatus.Sent, row.Status));
    }

    [PostgresFact]
    public async Task Position_is_a_snapshot_and_missing_gps_uses_the_last_measurement_before_the_trigger()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var old = await Register(senior);
        var at = Now().AddMinutes(-2);
        await Status(await Upload(senior, old.DeviceId, Sample(at)), 200);
        await Status(await senior.Client.DeleteAsync(Devices + "/" + old.DeviceId), 204);
        var current = await Register(senior);
        await Status(await Upload(senior, current.DeviceId, Sample(Now())), 200);
        var request = new TriggerSosRequest(current.DeviceId, Now().AddMinutes(-1), null);
        var response = await Trigger(senior, senior.Id, request);
        await Status(response, 201);
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal(at, incident.Position!.CapturedAt);
        Assert.Equal(52.2297, incident.Position.Coordinates.Latitude);
        await Status(await Upload(senior, current.DeviceId, Sample(Now().AddSeconds(1))), 200);
        var read = (await senior.Client.GetFromJsonAsync<IncidentDto>(Path(senior.Id, incident.IncidentId)))!;
        Assert.Equal(incident.Position, read.Position);
    }

    [PostgresFact]
    public async Task Supplied_position_takes_precedence_and_dates_are_normalized_to_utc_milliseconds()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        await Status(await Upload(senior, device.DeviceId, Sample(Now())), 200);
        var at = Now().AddTicks(1234).ToOffset(TimeSpan.FromHours(2));
        var response = await Trigger(senior, senior.Id, new(device.DeviceId, at, new(new(50, 19), 3, null, at)));
        await Status(response, 201);
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(at.ToUnixTimeMilliseconds()), incident.OccurredAt);
        Assert.Equal(TimeSpan.Zero, incident.OccurredAt.Offset);
        Assert.Equal(50, incident.Position!.Coordinates.Latitude);
        Assert.Equal(incident.OccurredAt, incident.Position.CapturedAt);
    }

    [PostgresFact]
    public async Task Repeated_triggers_share_an_active_alarm_and_replays_stay_on_the_original_after_resolution()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var first = await Register(senior);
        var second = await Register(senior, false);
        var original = new TriggerSosRequest(first.DeviceId, Now(), null);
        var repeated = new TriggerSosRequest(second.DeviceId, original.TriggeredAt.AddMilliseconds(10), null);
        var created = (await (await Trigger(senior, senior.Id, original)).Content.ReadFromJsonAsync<IncidentDto>())!;
        var replay = await Trigger(senior, senior.Id, original);
        await Status(replay, 200);
        var folded = await Trigger(senior, senior.Id, repeated);
        await Status(folded, 200);
        Assert.Equal(created.IncidentId, (await folded.Content.ReadFromJsonAsync<IncidentDto>())!.IncidentId);
        var resolved = await caretaker.Client.PostAsJsonAsync(Path(senior.Id, created.IncidentId) + "/resolve", new { note = "Gotowe" });
        await Status(resolved, 200);
        await Status(await senior.Client.DeleteAsync(Devices + "/" + second.DeviceId), 204);
        var after = await Trigger(senior, senior.Id, repeated);
        await Status(after, 200);
        var dto = (await after.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal(created.IncidentId, dto.IncidentId);
        Assert.Equal("resolved", dto.Status);
        var fresh = await Trigger(senior, senior.Id, original with { TriggeredAt = original.TriggeredAt.AddSeconds(1) });
        await Status(fresh, 201);
        Assert.NotEqual(created.IncidentId, (await fresh.Content.ReadFromJsonAsync<IncidentDto>())!.IncidentId);
        Assert.Equal(2, factory.Services.GetRequiredService<RecordingSignal>().Count);
    }

    [PostgresFact]
    public async Task Concurrent_triggers_never_create_multiple_active_incidents_or_duplicate_notifications()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var request = new TriggerSosRequest(device.DeviceId, Now(), null);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Trigger(senior, senior.Id, request)));
        Assert.Single(results, row => row.StatusCode == HttpStatusCode.Created);
        Assert.All(results, row => Assert.Contains((int)row.StatusCode, new[] { 200, 201, 409 }));
        await Status(await Trigger(senior, senior.Id, request), 200);
        await Process(factory);
        Assert.Single((await Inbox(caretaker)).Items);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.Single(await context.SosIncidents.Where(row => row.SeniorId == senior.Id).ToListAsync());
        Assert.Single(await context.SosTriggerReceipts.Where(row => row.DeviceId == device.DeviceId).ToListAsync());
    }

    [PostgresFact]
    public async Task Caretakers_can_acknowledge_and_resolve_and_first_values_are_preserved()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var first = await Actor(factory, AccountRole.Caretaker);
        using var second = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, first);
        await Link(senior, second);
        var device = await Register(senior);
        var incident = (await (await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null))).Content.ReadFromJsonAsync<IncidentDto>())!;
        var path = Path(senior.Id, incident.IncidentId);
        var acknowledgements = await Task.WhenAll(first.Client.PostAsync(path + "/acknowledge", null), second.Client.PostAsync(path + "/acknowledge", null));
        var replies = new List<IncidentDto>();
        foreach (var response in acknowledgements) { await Status(response, 200); replies.Add((await response.Content.ReadFromJsonAsync<IncidentDto>())!); }
        Assert.Single(replies.Select(row => row.AcknowledgedBy!.UserId).Distinct());
        Assert.Single(replies.Select(row => row.AcknowledgedAt).Distinct());
        var resolved = await second.Client.PostAsJsonAsync(path + "/resolve", new { note = "  Jest bezpiecznie  " });
        await Status(resolved, 200);
        var finished = (await resolved.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal("resolved", finished.Status);
        Assert.Equal(second.Id, finished.ResolvedBy!.UserId);
        Assert.Equal("Jest bezpiecznie", finished.ResolutionNote);
        var again = await first.Client.PostAsJsonAsync(path + "/resolve", new { note = "Nadpisanie" });
        await Status(again, 200);
        Assert.Equal(finished, (await again.Content.ReadFromJsonAsync<IncidentDto>())!);
        var lateAck = await first.Client.PostAsync(path + "/acknowledge", null);
        await Status(lateAck, 200);
        Assert.Equal(finished, (await lateAck.Content.ReadFromJsonAsync<IncidentDto>())!);
    }

    [PostgresFact]
    public async Task Concurrent_acknowledge_and_resolve_never_reopen_an_incident()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var incident = (await (await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null))).Content.ReadFromJsonAsync<IncidentDto>())!;
        var path = Path(senior.Id, incident.IncidentId);
        var replies = await Task.WhenAll(caretaker.Client.PostAsync(path + "/acknowledge", null),
            caretaker.Client.PostAsJsonAsync(path + "/resolve", new { note = (string?)null }));
        foreach (var response in replies) await Status(response, 200);
        Assert.Equal("resolved", (await caretaker.Client.GetFromJsonAsync<IncidentDto>(path))!.Status);
    }

    [PostgresFact]
    public async Task Roles_ownership_current_relationships_and_device_retirement_are_enforced()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var other = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var device = await Register(senior);
        var foreign = await Register(other);
        var request = new TriggerSosRequest(device.DeviceId, Now(), null);
        await Error(await Trigger(caretaker, senior.Id, request), 403, "operationForbidden");
        await Error(await Trigger(other, senior.Id, request), 404, "resourceNotFound");
        await Error(await Trigger(senior, senior.Id, request with { DeviceId = foreign.DeviceId }), 404, "resourceNotFound");
        var response = await Trigger(senior, senior.Id, request);
        await Status(response, 201);
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        var path = Path(senior.Id, incident.IncidentId);
        await Error(await senior.Client.PostAsync(path + "/acknowledge", null), 403, "operationForbidden");
        await Error(await other.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await caretaker.Client.GetAsync(Path(other.Id, incident.IncidentId)), 404, "resourceNotFound");
        await Status(await caretaker.Client.DeleteAsync("/api/v1/care-relationships/" + relationship.RelationshipId), 204);
        await Error(await caretaker.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsync(path + "/acknowledge", null), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync(path + "/resolve", new { note = (string?)null }), 404, "resourceNotFound");
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
        await Status(await senior.Client.DeleteAsync(Devices + "/" + device.DeviceId), 204);
        await Error(await Trigger(senior, senior.Id, request with { TriggeredAt = request.TriggeredAt.AddSeconds(1) }), 404, "resourceNotFound");
        await Status(await Trigger(senior, senior.Id, request), 200);
    }

    [PostgresFact]
    public async Task Acknowledgement_cancels_unsent_notifications_and_reading_a_notification_does_not_acknowledge_sos()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var response = await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null));
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        await Process(factory);
        var item = Assert.Single((await Inbox(caretaker)).Items);
        await Status(await caretaker.Client.PutAsync("/api/v1/me/notifications/" + item.NotificationId + "/read", null), 200);
        Assert.Equal("open", (await caretaker.Client.GetFromJsonAsync<IncidentDto>(Path(senior.Id, incident.IncidentId)))!.Status);
        await Status(await caretaker.Client.PostAsync(Path(senior.Id, incident.IncidentId) + "/acknowledge", null), 200);
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetServices<INotificationEventSource>().Single(source => source.Name == SafetyNotificationEvents.Source);
        var events = await scope.ServiceProvider.GetRequiredService<SafetyDbContext>().OutboxMessages.Where(row => row.Type == SafetyNotificationEvents.SosTriggered).ToListAsync();
        var own = events.Select(row => JsonSerializer.Deserialize<NotificationEvent>(row.Payload)!).Single(row => row.TargetId == incident.IncidentId);
        Assert.False(await source.IsRelevantAsync(own, CancellationToken.None));
    }

    [PostgresFact]
    public async Task Acknowledgement_before_processing_suppresses_outbox_messages()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var response = await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null));
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        await Status(await caretaker.Client.PostAsync(Path(senior.Id, incident.IncidentId) + "/acknowledge", null), 200);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
    }

    [PostgresFact]
    public async Task Invalid_positions_dates_and_filters_do_not_write_partial_incidents()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        var valid = new TriggerSosRequest(device.DeviceId, Now(), null);
        var position = new IncidentPositionDto(new(50, 19), 5, 0, valid.TriggeredAt);
        foreach (var request in new[] { valid with { DeviceId = Guid.Empty }, valid with { TriggeredAt = Now().AddDays(-31) },
            valid with { TriggeredAt = Now().AddMinutes(6) }, valid with { Position = position with { Coordinates = new(91, 19) } },
            valid with { Position = position with { AccuracyMeters = -1 } }, valid with { Position = position with { CapturedAt = valid.TriggeredAt.AddSeconds(1) } } })
            await Error(await Trigger(senior, senior.Id, request), 400, "validationFailed");
        await Error(await senior.Client.GetAsync(Base(senior.Id) + "/incidents?limit=0"), 400, "validationFailed");
        await Error(await senior.Client.GetAsync(Base(senior.Id) + "/incidents?type=bad"), 400, "validationFailed");
        await Error(await senior.Client.GetAsync(Base(senior.Id) + "/incidents?from=2026-10-20T00:00:00Z&to=2026-10-19T00:00:00Z"), 400, "validationFailed");
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<SafetyDbContext>().SosIncidents.AnyAsync(row => row.SeniorId == senior.Id));
    }

    [PostgresFact]
    public async Task History_uses_pagination_status_date_and_type_filters()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var at = Now();
        for (var i = 0; i < 3; i++)
        {
            var response = await Trigger(senior, senior.Id, new(device.DeviceId, at.AddMilliseconds(i), null));
            await Status(response, 201);
            var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
            if (i < 2) await Status(await caretaker.Client.PostAsJsonAsync(Path(senior.Id, incident.IncidentId) + "/resolve", new { note = (string?)null }), 200);
        }
        var first = (await caretaker.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?limit=1"))!;
        var second = (await caretaker.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?limit=1&page=2"))!;
        Assert.Equal(3, first.TotalCount);
        Assert.NotEqual(first.Items[0].IncidentId, second.Items[0].IncidentId);
        var filtered = (await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?status=resolved&type=sos"))!;
        Assert.Equal(2, filtered.TotalCount);
        Assert.Empty((await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?type=lowBattery"))!.Items);
        var from = Uri.EscapeDataString(at.AddMinutes(-1).ToOffset(TimeSpan.FromHours(2)).ToString("O"));
        var to = Uri.EscapeDataString(at.AddMinutes(1).ToOffset(TimeSpan.FromHours(2)).ToString("O"));
        Assert.Equal(3, (await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?from=" + from + "&to=" + to))!.TotalCount);
    }

    [PostgresFact]
    public async Task Concurrent_resolutions_keep_the_first_author_time_and_note()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var first = await Actor(factory, AccountRole.Caretaker);
        using var second = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, first);
        await Link(senior, second);
        var device = await Register(senior);
        var response = await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null));
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        var path = Path(senior.Id, incident.IncidentId) + "/resolve";
        var responses = await Task.WhenAll(first.Client.PostAsJsonAsync(path, new { note = "Pierwszy" }), second.Client.PostAsJsonAsync(path, new { note = "Drugi" }));
        var values = new List<IncidentDto>();
        foreach (var result in responses) { await Status(result, 200); values.Add((await result.Content.ReadFromJsonAsync<IncidentDto>())!); }
        Assert.Equal(values[0], values[1]);
    }

    [PostgresFact]
    public async Task Deactivated_caretakers_do_not_receive_new_events()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        await using (var context = fixture.Context())
        {
            var row = (await context.Accounts.FindAsync(caretaker.Id))!;
            row.Status = AccountStatus.PendingDeletion;
            await context.SaveChangesAsync();
        }
        var created = await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null));
        await Status(created, 201);
        await using var scope = factory.Services.CreateAsyncScope();
        var events = await scope.ServiceProvider.GetRequiredService<SafetyDbContext>().OutboxMessages.Where(row => row.Type == SafetyNotificationEvents.SosTriggered).ToListAsync();
        Assert.DoesNotContain(events, row => JsonSerializer.Deserialize<NotificationEvent>(row.Payload)!.SeniorId == senior.Id);
    }

    [PostgresFact]
    public async Task Cleanup_removes_receipts_outbox_and_incidents_without_cascades()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        await Status(await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null)), 201);
        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>()) await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.False(await db.SosIncidents.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await db.SosTriggerReceipts.AnyAsync(row => row.DeviceId == device.DeviceId));
        var json = JsonSerializer.Serialize(new { SeniorId = senior.Id });
        Assert.False(await db.OutboxMessages.AnyAsync(row => EF.Functions.JsonContains(row.Payload, json)));
    }

    [PostgresFact]
    public async Task Anonymous_requests_cannot_access_any_incident_endpoint()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var id = Guid.CreateVersion7();
        await Error(await client.GetAsync(Base(id) + "/incidents"), 401, "unauthenticated");
        await Error(await client.GetAsync(Path(id, id)), 401, "unauthenticated");
        await Error(await client.PostAsJsonAsync(Base(id) + "/sos-incidents", new { deviceId = id, triggeredAt = Now(), position = (object?)null }), 401, "unauthenticated");
        await Error(await client.PostAsync(Path(id, id) + "/acknowledge", null), 401, "unauthenticated");
        await Error(await client.PostAsJsonAsync(Path(id, id) + "/resolve", new { note = (string?)null }), 401, "unauthenticated");
    }


    [PostgresFact]
    public async Task Permanently_deleted_caretaker_leaves_the_incident_history_and_timestamps()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var response = await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null));
        await Status(response, 201);
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        var path = Path(senior.Id, incident.IncidentId);
        await Status(await caretaker.Client.PostAsync(path + "/acknowledge", null), 200);
        var resolved = await caretaker.Client.PostAsJsonAsync(path + "/resolve", new { note = "Bezpiecznie" });
        await Status(resolved, 200);
        var before = (await resolved.Content.ReadFromJsonAsync<IncidentDto>())!;
        await using (var scope = factory.Services.CreateAsyncScope())
            foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>()) await cleanup.DeleteAsync(caretaker.Id, CancellationToken.None);
        await using (var context = fixture.Context())
        {
            var account = (await context.Accounts.FindAsync(caretaker.Id))!;
            context.Accounts.Remove(account);
            await context.SaveChangesAsync();
        }
        var after = (await senior.Client.GetFromJsonAsync<IncidentDto>(path))!;
        Assert.Equal("resolved", after.Status);
        Assert.Equal(before.AcknowledgedAt, after.AcknowledgedAt);
        Assert.Equal(before.ResolvedAt, after.ResolvedAt);
        Assert.Null(after.AcknowledgedBy);
        Assert.Null(after.ResolvedBy);
        Assert.Equal("Bezpiecznie", after.ResolutionNote);
    }

    [PostgresFact]
    public async Task Broken_outbox_events_are_retried_without_blocking_other_sos_deliveries()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var malformed = Guid.CreateVersion7();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
            db.OutboxMessages.Add(new Zpi.Database.Outbox.OutboxMessage
            {
                Id = malformed,
                Type = SafetyNotificationEvents.SosTriggered,
                ContractVersion = 99,
                Payload = "{}",
                OccurredAt = Now()
            });
            await db.SaveChangesAsync();
        }
        await Status(await Trigger(senior, senior.Id, new(device.DeviceId, Now(), null)), 201);
        await Process(factory);
        Assert.Single((await Inbox(caretaker)).Items);
        await using var check = factory.Services.CreateAsyncScope();
        var row = (await check.ServiceProvider.GetRequiredService<SafetyDbContext>().OutboxMessages.FindAsync(malformed))!;
        Assert.Null(row.ProcessedAt);
        Assert.Equal(1, row.Attempts);
        Assert.NotNull(row.NextAttemptAt);
    }


    [PostgresFact]
    public async Task Optional_gps_and_resolution_note_can_be_omitted_from_json()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var response = await senior.Client.PostAsJsonAsync(Base(senior.Id) + "/sos-incidents", new { deviceId = device.DeviceId, triggeredAt = Now() });
        await Status(response, 201);
        var incident = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
        var resolved = await caretaker.Client.PostAsJsonAsync(Path(senior.Id, incident.IncidentId) + "/resolve", new { });
        await Status(resolved, 200);
        var result = (await resolved.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal("resolved", result.Status);
        Assert.Null(result.ResolutionNote);
        Assert.Null(result.AcknowledgedAt);
    }

    private WebApplicationFactory<Program> Factory() => fixture.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
        services.AddSingleton<RecordingSms>();
        services.AddSingleton<ISmsNotificationSender>(provider => provider.GetRequiredService<RecordingSms>());
        services.AddSingleton<RecordingSignal>();
        services.AddSingleton<INotificationProcessingSignal>(provider => provider.GetRequiredService<RecordingSignal>());
    }));
    private static string Base(Guid seniorId) => $"/api/v1/seniors/{seniorId}";
    private static string Path(Guid seniorId, Guid incidentId) => Base(seniorId) + "/incidents/" + incidentId;
    private static Task<HttpResponseMessage> Trigger(TestActor actor, Guid seniorId, TriggerSosRequest request) =>
        actor.Client.PostAsJsonAsync(Base(seniorId) + "/sos-incidents", request);
    private static async Task<PageDto<NotificationDto>> Inbox(TestActor actor) =>
        (await actor.Client.GetFromJsonAsync<PageDto<NotificationDto>>("/api/v1/me/notifications?type=sos"))!;
    private static async Task Process(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationProcessor>().ProcessAsync(CancellationToken.None);
    }
    private static async Task Status(HttpResponseMessage response, int status) =>
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private sealed class RecordingSignal : INotificationProcessingSignal
    {
        public int Count { get; private set; }
        public void RequestProcessing() => Count++;
    }
    private sealed class RecordingSms : ISmsNotificationSender
    {
        public Task<DeliverySendResult> SendAsync(string idempotencyKey, string phoneNumber, string message, CancellationToken cancellationToken) =>
            Task.FromResult(new DeliverySendResult(true, "test:" + idempotencyKey));
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new TestActor(account.Id, phone, client);
    }

    private static async Task<CareRelationshipDto> Link(TestActor senior, TestActor caretaker)
    {
        var created = await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = senior.Phone });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invitation = (await created.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }

    private static DateTimeOffset Now() => DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    private static RegisterDeviceRequest Registration(bool monitoring = true) => new(Guid.CreateVersion7(), DevicePlatform.Android, "1.0.0", monitoring);
    private static Task<DeviceDto> Register(TestActor actor, bool monitoring = true) => Register(actor, Registration(monitoring));
    private static async Task<DeviceDto> Register(TestActor actor, RegisterDeviceRequest request)
    {
        var response = await actor.Client.PostAsJsonAsync(Devices, request, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeviceDto>())!;
    }
    private static TelemetrySample Sample(DateTimeOffset at) => new(at, new(new(52.2297, 21.0122), 8.5, 0), new(75, false));
    private static Task<HttpResponseMessage> Upload(TestActor actor, Guid deviceId, params TelemetrySample?[] samples) =>
        actor.Client.PostAsJsonAsync($"{Devices}/{deviceId}/telemetry", new UploadTelemetryRequest(samples));
    private static async Task Error(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
    private sealed record TestActor(Guid Id, string Phone, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
