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
using Microsoft.Extensions.Configuration;
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
using Zpi.Modules.Safety.Application.Geozones;
using Zpi.Modules.Safety.Application.Home;
using Zpi.Modules.Safety.Application.Incidents.Trigger;
using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Application.Notifications;
using Zpi.Modules.Safety.Application.Telemetry.Upload;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class GeofenceTests(IdentityApiFixture fixture)
{
    private const string Password = "SafetyPassword123!";
    private const string Devices = "/api/v1/me/devices";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [PostgresFact]
    public async Task Home_is_a_versioned_singleton_for_the_senior_and_active_caretakers()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var stranger = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var path = Base(senior.Id) + "/home-address";
        var body = new { countryCode = "PL", postalCode = "00-001", city = " Warszawa ", street = "Prosta", buildingNumber = "1", apartmentNumber = (string?)null, coordinates = new { latitude = 52.0, longitude = 21.0 } };
        await Status(await senior.Client.GetAsync(path), 404);
        await Status(await senior.Client.PutAsJsonAsync(path, body), 428);
        var created = await Send(senior, HttpMethod.Put, path, body, "If-None-Match", "*");
        await Status(created, 201);
        var home = (await created.Content.ReadFromJsonAsync<HomeAddressDto>())!;
        Assert.Equal("Warszawa", home.City);
        Assert.Equal("\"1\"", created.Headers.ETag!.Tag);
        await Status(await Send(caretaker, HttpMethod.Put, path, body, "If-None-Match", "*"), 412);
        await Status(await Send(caretaker, HttpMethod.Put, path, body, "If-Match", "\"1\""), 200);
        await Status(await Send(senior, HttpMethod.Put, path, body, "If-Match", "\"1\""), 412);
        await Status(await stranger.Client.GetAsync(path), 404);
        Assert.Empty(await Zones(senior));
        Assert.True((await caretaker.Client.GetAsync(path)).Headers.CacheControl!.NoStore);
    }

    [PostgresFact]
    public async Task Geozone_crud_checks_roles_etags_filters_and_soft_deletion()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var body = Zone();
        await Status(await senior.Client.PostAsJsonAsync(Base(senior.Id) + "/geozones", body), 403);
        var zone = await CreateZone(caretaker, senior.Id, body);
        var path = Base(senior.Id) + "/geozones/" + zone.ZoneId;
        await Status(await caretaker.Client.PutAsJsonAsync(path, body), 428);
        await Status(await Send(caretaker, HttpMethod.Put, path, body with { IsActive = false }, "If-Match", "\"1\""), 200);
        await Status(await Send(caretaker, HttpMethod.Delete, path, null, "If-Match", "\"1\""), 412);
        Assert.Empty((await senior.Client.GetFromJsonAsync<GeoZoneDto[]>(Base(senior.Id) + "/geozones?isActive=true"))!);
        await Status(await Send(caretaker, HttpMethod.Delete, path, null, "If-Match", "\"2\""), 204);
        await Status(await senior.Client.GetAsync(path), 404);
        Assert.Empty(await Zones(senior));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.NotNull((await scope.ServiceProvider.GetRequiredService<SafetyDbContext>().GeoZones.FindAsync(zone.ZoneId))!.DeletedAt);
    }

    [PostgresFact]
    public async Task Invalid_coordinates_radius_and_address_are_rejected_and_zone_limit_is_enforced()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Error(await caretaker.Client.PostAsJsonAsync(Base(senior.Id) + "/geozones", Zone() with { RadiusMeters = 49 }), 400, "validationFailed");
        await Status(await caretaker.Client.PostAsJsonAsync(Base(senior.Id) + "/geozones", Zone() with { Center = new(91, 21) }), 400);
        for (var i = 0; i < 20; i++) await CreateZone(caretaker, senior.Id, Zone() with { Name = "Zone " + i });
        await Error(await caretaker.Client.PostAsJsonAsync(Base(senior.Id) + "/geozones", Zone()), 409, "zoneLimitReached");
        var invalid = new { countryCode = "Poland", postalCode = "", city = " ", buildingNumber = "", coordinates = new { latitude = 91, longitude = 21 } };
        await Status(await Send(senior, HttpMethod.Put, Base(senior.Id) + "/home-address", invalid, "If-None-Match", "*"), 400);
    }

    [PostgresFact]
    public async Task Overlapping_zones_form_one_area_and_exit_snapshots_the_last_inside_zones()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var second = await CreateZone(caretaker, senior.Id, Zone() with { Name = "Park", Center = new(52, 21.004) });
        var device = await Register(senior);
        var at = Now().AddMinutes(-4);
        await Status(await Upload(senior, device.DeviceId, Gps(at, 21), Gps(at.AddSeconds(30), 21),
            Gps(at.AddSeconds(60), 21.006), Gps(at.AddSeconds(90), 21.006)), 200);
        Assert.Empty((await Incidents(senior)).Items);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(120), 21.03), Gps(at.AddSeconds(150), 21.03)), 200);
        var incident = Assert.Single((await Incidents(senior)).Items);
        Assert.Equal("geofenceExit", incident.Type);
        Assert.Equal(second.ZoneId, incident.Details.ZoneId);
        Assert.Equal("Park", Assert.Single(incident.Details.Zones!).Name);
        await Status(await Send(caretaker, HttpMethod.Put, Base(senior.Id) + "/geozones/" + second.ZoneId,
            Zone() with { Name = "Changed" }, "If-Match", "\"1\""), 200);
        var historical = (await senior.Client.GetFromJsonAsync<IncidentDto>(Path(senior.Id, incident.IncidentId)))!;
        Assert.Equal("Park", historical.Details.ZoneName);
        Assert.Equal(at.AddSeconds(150), historical.Position!.CapturedAt);
    }

    [PostgresFact]
    public async Task Sms_setting_comes_from_the_last_inside_zones_and_incidents_are_always_recorded()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone() with { SmsOnExitEnabled = false });
        await CreateZone(caretaker, senior.Id, Zone() with { Center = new(53, 21), SmsOnExitEnabled = true });
        var device = await Register(senior);
        await Exit(senior, device.DeviceId, Now().AddMinutes(-3));
        Assert.Single((await Incidents(senior)).Items);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
    }

    [PostgresFact]
    public async Task Initial_outside_ambiguous_and_missing_accuracy_do_not_create_false_exits()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-4);
        await Status(await Upload(senior, device.DeviceId, Gps(at, 21.03), Gps(at.AddSeconds(30), 21.03),
            Gps(at.AddSeconds(60), 21), Gps(at.AddSeconds(90), 21), Gps(at.AddSeconds(120), 21.03),
            Gps(at.AddSeconds(150), 21.03, null), Gps(at.AddSeconds(180), 21.03)), 200);
        Assert.Empty((await Incidents(senior)).Items);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(210), 21.03)), 200);
        Assert.Single((await Incidents(senior)).Items);
    }

    [PostgresFact]
    public async Task Confirmation_survives_new_scopes_and_duplicates_and_late_samples_cannot_rewind_it()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-3);
        await Status(await Upload(senior, device.DeviceId, Gps(at, 21), Gps(at.AddSeconds(30), 21)), 200);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(60), 21.03)), 200);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var state = (await scope.ServiceProvider.GetRequiredService<SafetyDbContext>().AutomaticAlertStates.FindAsync(senior.Id))!;
            Assert.Equal(at.AddSeconds(60), state.CandidateSince);
        }
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(60), 21.03), Gps(at.AddSeconds(45), 21)), 200);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(90), 21.03)), 200);
        var incident = Assert.Single((await Incidents(senior)).Items);
        Assert.Equal(at.AddSeconds(90), incident.OccurredAt);
    }

    [PostgresFact]
    public async Task Reversed_batches_are_evaluated_in_capture_order_and_publish_once_per_caretaker()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var first = await Actor(factory, AccountRole.Caretaker);
        using var second = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, first);
        await Link(senior, second);
        await CreateZone(first, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-3);
        var samples = new[] { Gps(at, 21), Gps(at.AddSeconds(30), 21), Gps(at.AddSeconds(60), 21.03), Gps(at.AddSeconds(90), 21.03) };
        await Status(await Upload(senior, device.DeviceId, samples.Reverse().ToArray()), 200);
        await Status(await Upload(senior, device.DeviceId, samples), 200);
        Assert.Single((await Incidents(senior)).Items);
        await Process(factory);
        await Process(factory);
        Assert.Single((await Inbox(first)).Items);
        Assert.Single((await Inbox(second)).Items);
    }

    [PostgresFact]
    public async Task Confirmed_return_permanently_cancels_the_pending_notification_for_the_open_incident()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-4);
        await Exit(senior, device.DeviceId, at);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(120), 21), Gps(at.AddSeconds(150), 21),
            Gps(at.AddSeconds(180), 21.03), Gps(at.AddSeconds(210), 21.03)), 200);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
        Assert.Equal("open", Assert.Single((await Incidents(senior)).Items).Status);
    }

    [PostgresFact]
    public async Task Closed_geofence_requires_a_return_before_another_incident_can_be_created()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-4);
        await Exit(senior, device.DeviceId, at);
        var incident = Assert.Single((await Incidents(senior)).Items);
        await Status(await caretaker.Client.PostAsJsonAsync(Path(senior.Id, incident.IncidentId) + "/resolve", new { note = "OK" }), 200);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(100), 21.03)), 200);
        Assert.Single((await Incidents(senior)).Items);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(120), 21), Gps(at.AddSeconds(150), 21),
            Gps(at.AddSeconds(180), 21.03), Gps(at.AddSeconds(210), 21.03)), 200);
        Assert.Equal(2, (await Incidents(senior)).TotalCount);
    }

    [PostgresFact]
    public async Task Low_battery_alert_has_hysteresis_and_charging_cancels_without_rearming()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var at = Now().AddMinutes(-3);
        await Status(await Upload(senior, device.DeviceId, Battery(at, 20)), 200);
        var incident = Assert.Single((await Incidents(senior)).Items);
        Assert.Equal("lowBattery", incident.Type);
        Assert.Equal(20, incident.Details.BatteryLevelPercent);
        Assert.Null(incident.Position);
        await Status(await Upload(senior, device.DeviceId, Battery(at.AddSeconds(10), 19, true)), 200);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
        await Status(await caretaker.Client.PostAsJsonAsync(Path(senior.Id, incident.IncidentId) + "/resolve", new { }), 200);
        await Status(await Upload(senior, device.DeviceId, Battery(at.AddSeconds(20), 21), Battery(at.AddSeconds(30), 20),
            Battery(at.AddSeconds(40), 29), Battery(at.AddSeconds(50), 19)), 200);
        Assert.Single((await Incidents(senior)).Items);
        await Status(await Upload(senior, device.DeviceId, Battery(at.AddSeconds(60), 30), Battery(at.AddSeconds(70), 20)), 200);
        Assert.Equal(2, (await Incidents(senior)).TotalCount);
        await Process(factory);
        Assert.Single((await Inbox(caretaker)).Items);
    }

    [PostgresFact]
    public async Task Rearming_while_an_incident_is_open_never_replays_its_notification()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var at = Now().AddMinutes(-2);
        await Status(await Upload(senior, device.DeviceId, Battery(at, 20), Battery(at.AddSeconds(10), 30), Battery(at.AddSeconds(20), 19)), 200);
        Assert.Single((await Incidents(senior)).Items);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
    }

    [PostgresFact]
    public async Task Stale_future_and_out_of_order_battery_samples_only_affect_history()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        await Status(await Upload(senior, device.DeviceId, Battery(Now().AddMinutes(-6), 1), Battery(Now().AddMinutes(1), 1)), 200);
        Assert.Empty((await Incidents(senior)).Items);
        var at = Now().AddMinutes(-2);
        await Status(await Upload(senior, device.DeviceId, Battery(at.AddSeconds(30), 80)), 200);
        await Status(await Upload(senior, device.DeviceId, Battery(at, 1)), 200);
        Assert.Empty((await Incidents(senior)).Items);
    }

    [PostgresFact]
    public async Task Editing_zones_and_switching_devices_reset_baselines_and_cancel_pending_alerts()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var zone = await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-3);
        await Exit(senior, device.DeviceId, at);
        await Status(await Send(caretaker, HttpMethod.Put, Base(senior.Id) + "/geozones/" + zone.ZoneId,
            Zone() with { RadiusMeters = 250 }, "If-Match", "\"1\""), 200);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
        var next = await Register(senior, false);
        await Status(await senior.Client.PostAsync(Devices + "/" + next.DeviceId + "/switch", null), 200);
        await Status(await Upload(senior, next.DeviceId, Gps(at.AddSeconds(120), 21.03), Gps(at.AddSeconds(150), 21.03)), 200);
        Assert.Single((await Incidents(senior)).Items);
    }

    [PostgresFact]
    public async Task Incidents_merge_types_before_pagination_and_all_types_support_idempotent_reactions()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-3);
        await Exit(senior, device.DeviceId, at);
        await Status(await Upload(senior, device.DeviceId, Battery(at.AddSeconds(100), 10)), 200);
        await Status(await Trigger(senior, senior.Id, new(device.DeviceId, at.AddSeconds(110), null)), 201);
        var all = await Incidents(senior);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(new[] { "geofenceExit", "lowBattery", "sos" }, all.Items.Select(row => row.Type).Order());
        var paged = (await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?page=2&limit=1"))!;
        Assert.Equal(3, paged.TotalCount);
        Assert.Single(paged.Items);
        Assert.Equal(all.Items[1].IncidentId, paged.Items[0].IncidentId);
        foreach (var incident in all.Items)
        {
            var response = await caretaker.Client.PostAsync(Path(senior.Id, incident.IncidentId) + "/acknowledge", null);
            await Status(response, 200);
            var acknowledged = (await response.Content.ReadFromJsonAsync<IncidentDto>())!;
            Assert.Equal("acknowledged", acknowledged.Status);
            Assert.Equal(caretaker.Id, acknowledged.AcknowledgedBy!.UserId);
            await Status(await caretaker.Client.PostAsJsonAsync(Path(senior.Id, incident.IncidentId) + "/resolve", new { note = " First " }), 200);
            var repeated = await caretaker.Client.PostAsJsonAsync(Path(senior.Id, incident.IncidentId) + "/resolve", new { note = "Other" });
            await Status(repeated, 200);
            Assert.Equal("First", (await repeated.Content.ReadFromJsonAsync<IncidentDto>())!.ResolutionNote);
        }
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
    }

    [PostgresFact]
    public async Task Concurrent_uploads_do_not_duplicate_incidents_or_outbox_and_schema_matches_migrations()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        var sample = Battery(Now().AddSeconds(-10), 10);
        var results = await Task.WhenAll(Upload(senior, device.DeviceId, sample), Upload(senior, device.DeviceId, sample));
        Assert.Contains(results, row => row.IsSuccessStatusCode);
        Assert.All(results, row => Assert.Contains((int)row.StatusCode, new[] { 200, 409 }));
        await Status(await Upload(senior, device.DeviceId, sample), 200);
        Assert.Single((await Incidents(senior)).Items);
        await Process(factory);
        Assert.Single((await Inbox(caretaker)).Items);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<SafetyDbContext>().Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task A_long_gap_starts_a_new_baseline_instead_of_reporting_a_stale_crossing()
    {
        var clock = new AdjustableClock(Now());
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        await Status(await Upload(senior, device.DeviceId, Gps(clock.Current.AddMinutes(-1), 21), Gps(clock.Current.AddSeconds(-30), 21)), 200);
        clock.Current = clock.Current.AddMinutes(6);
        await Status(await Upload(senior, device.DeviceId, Gps(clock.Current.AddSeconds(-30), 21.03), Gps(clock.Current, 21.03)), 200);
        Assert.Empty((await Incidents(senior)).Items);
    }

    [PostgresFact]
    public async Task Border_uncertainty_and_short_confirmation_intervals_interrupt_exit_confirmation()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var at = Now().AddMinutes(-3);
        await Status(await Upload(senior, device.DeviceId, Gps(at, 21), Gps(at.AddSeconds(30), 21), Gps(at.AddSeconds(60), 21.03),
            Gps(at.AddSeconds(89), 21.03), Gps(at.AddSeconds(90), 21.0029, 100), Gps(at.AddSeconds(100), 21.03)), 200);
        Assert.Empty((await Incidents(senior)).Items);
        await Status(await Upload(senior, device.DeviceId, Gps(at.AddSeconds(130), 21.03)), 200);
        Assert.Single((await Incidents(senior)).Items);
    }

    [PostgresFact]
    public async Task Loss_of_care_relationship_revokes_configuration_history_and_pending_notifications()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var zone = await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        await Exit(senior, device.DeviceId, Now().AddMinutes(-3));
        var incident = Assert.Single((await Incidents(senior)).Items);
        await Status(await caretaker.Client.DeleteAsync("/api/v1/care-relationships/" + relationship.RelationshipId), 204);
        await Status(await caretaker.Client.GetAsync(Base(senior.Id) + "/geozones"), 404);
        await Status(await caretaker.Client.PostAsJsonAsync(Base(senior.Id) + "/geozones", Zone()), 404);
        await Status(await caretaker.Client.GetAsync(Path(senior.Id, incident.IncidentId)), 404);
        await Status(await caretaker.Client.PostAsync(Path(senior.Id, incident.IncidentId) + "/acknowledge", null), 404);
        await Process(factory);
        Assert.Empty((await Inbox(caretaker)).Items);
        await Status(await Send(senior, HttpMethod.Delete, Base(senior.Id) + "/geozones/" + zone.ZoneId, null, "If-Match", "\"1\""), 403);
    }

    [PostgresFact]
    public async Task Battery_position_is_the_last_sample_before_the_event_and_filters_work_across_types()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        var at = Now().AddMinutes(-2);
        await Status(await Upload(senior, device.DeviceId, Gps(at, 21), Gps(at.AddSeconds(40), 21.03), Battery(at.AddSeconds(20), 19)), 200);
        var incident = Assert.Single((await Incidents(senior)).Items);
        Assert.Equal(at, incident.Position!.CapturedAt);
        Assert.Equal(21, incident.Position.Coordinates.Longitude);
        Assert.Single((await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?type=lowBattery&status=open"))!.Items);
        Assert.Empty((await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents?type=geofenceExit"))!.Items);
    }

    [PostgresFact]
    public async Task Cleanup_removes_detector_state_incidents_and_events_but_preserves_other_seniors()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        await Exit(senior, device.DeviceId, Now().AddMinutes(-3));
        await Status(await Upload(senior, device.DeviceId, Battery(Now().AddMinutes(-1), 10)), 200);
        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>()) await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.False(await db.AutomaticAlertStates.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await db.GeoZones.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await db.GeofenceIncidents.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await db.LowBatteryIncidents.AnyAsync(row => row.SeniorId == senior.Id));
        var events = await db.OutboxMessages.Where(row => SafetyNotificationEvents.Types.Contains(row.Type)).ToListAsync();
        Assert.DoesNotContain(events, row => JsonSerializer.Deserialize<NotificationEvent>(row.Payload)!.SeniorId == senior.Id);
    }

    [PostgresFact]
    public async Task Anonymous_clients_cannot_read_or_edit_home_and_geozones()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var path = Base(Guid.CreateVersion7());
        await Error(await client.GetAsync(path + "/home-address"), 401, "unauthenticated");
        await Error(await client.GetAsync(path + "/geozones"), 401, "unauthenticated");
        await Error(await client.PostAsJsonAsync(path + "/geozones", Zone()), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Alert_thresholds_and_intervals_are_read_from_configuration()
    {
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Safety:Alerts:Freshness"] = "00:01:00",
                ["Safety:Alerts:Confirmation"] = "00:00:10",
                ["Safety:Alerts:LowBatteryPercent"] = "25",
                ["Safety:Alerts:BatteryRearmPercent"] = "40"
            })));
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await CreateZone(caretaker, senior.Id, Zone());
        var device = await Register(senior);
        var now = Now();
        await Status(await Upload(senior, device.DeviceId, Battery(now.AddSeconds(-90), 1)), 200);
        Assert.Empty((await Incidents(senior)).Items);
        await Status(await Upload(senior, device.DeviceId, Gps(now.AddSeconds(-50), 21), Gps(now.AddSeconds(-40), 21),
            Gps(now.AddSeconds(-20), 21.03), Gps(now.AddSeconds(-10), 21.03), Battery(now.AddSeconds(-35), 24)), 200);
        var incidents = (await Incidents(senior)).Items;
        Assert.Equal(2, incidents.Count);
        var battery = Assert.Single(incidents, row => row.Type == "lowBattery");
        Assert.Equal(24, battery.Details.BatteryLevelPercent);
        await Status(await caretaker.Client.PostAsJsonAsync(Path(senior.Id, battery.IncidentId) + "/resolve", new { }), 200);
        await Status(await Upload(senior, device.DeviceId, Battery(now.AddSeconds(-30), 30), Battery(now.AddSeconds(-25), 24)), 200);
        Assert.Equal(2, (await Incidents(senior)).TotalCount);
        await Status(await Upload(senior, device.DeviceId, Battery(now.AddSeconds(-20), 40), Battery(now.AddSeconds(-15), 24)), 200);
        Assert.Equal(3, (await Incidents(senior)).TotalCount);
    }

    private sealed class AdjustableClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private static SaveGeoZoneRequest Zone() => new("Dom", new(52, 21), 200, true, true);
    private static TelemetrySample Gps(DateTimeOffset at, double longitude, double? accuracy = 5) => new(at, new(new(52, longitude), accuracy, null), null);
    private static TelemetrySample Battery(DateTimeOffset at, int level, bool charging = false) => new(at, null, new(level, charging));
    private static Task<PageDto<IncidentDto>> Incidents(TestActor senior) => ReadIncidents(senior);
    private static async Task<PageDto<IncidentDto>> ReadIncidents(TestActor senior) => (await senior.Client.GetFromJsonAsync<PageDto<IncidentDto>>(Base(senior.Id) + "/incidents"))!;
    private static async Task<GeoZoneDto[]> Zones(TestActor senior) => (await senior.Client.GetFromJsonAsync<GeoZoneDto[]>(Base(senior.Id) + "/geozones"))!;
    private static async Task<GeoZoneDto> CreateZone(TestActor actor, Guid seniorId, SaveGeoZoneRequest request)
    {
        var response = await actor.Client.PostAsJsonAsync(Base(seniorId) + "/geozones", request);
        await Status(response, 201);
        return (await response.Content.ReadFromJsonAsync<GeoZoneDto>())!;
    }
    private static async Task Exit(TestActor actor, Guid deviceId, DateTimeOffset at) =>
        await Status(await Upload(actor, deviceId, Gps(at, 21), Gps(at.AddSeconds(30), 21), Gps(at.AddSeconds(60), 21.03), Gps(at.AddSeconds(90), 21.03)), 200);
    private static Task<HttpResponseMessage> Send(TestActor actor, HttpMethod method, string path, object? body, string header, string value)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(header, value);
        if (body is not null) request.Content = JsonContent.Create(body);
        return actor.Client.SendAsync(request);
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
        (await actor.Client.GetFromJsonAsync<PageDto<NotificationDto>>("/api/v1/me/notifications"))!;
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
