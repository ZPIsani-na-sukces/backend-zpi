using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Safety.Application.Devices;
using Zpi.Modules.Safety.Application.Devices.Register;
using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Application.Locations.Latest;
using Zpi.Modules.Safety.Application.Telemetry.Upload;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class SafetyTests(IdentityApiFixture fixture)
{
    private const string Password = "SafetyPass123!";
    private const string Devices = "/api/v1/me/devices";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [PostgresFact]
    public async Task Devices_return_the_latest_battery_even_when_older_telemetry_arrives_later()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        Assert.Null(device.LatestBattery);
        Assert.Null(Assert.Single((await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!).LatestBattery);
        var now = Now();
        Assert.Equal(HttpStatusCode.OK, (await Upload(senior, device.DeviceId,
            Sample(now) with { Battery = new(42, true) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Upload(senior, device.DeviceId,
            Sample(now.AddMinutes(-1)) with { Battery = new(75, false) },
            Sample(now.AddSeconds(1)) with { Battery = null })).StatusCode);
        var latest = Assert.Single((await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!).LatestBattery;
        Assert.Equal(new DeviceBatteryStatusDto(42, true, now), latest);
        var switched = await senior.Client.PostAsync($"{Devices}/{device.DeviceId}/switch", null);
        Assert.Equal(latest, (await switched.Content.ReadFromJsonAsync<DeviceDto>())!.LatestBattery);
    }

    [PostgresFact]
    public async Task Registration_updates_the_same_installation_and_caretakers_can_only_register_without_monitoring()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var request = Registration();
        var created = await senior.Client.PostAsJsonAsync(Devices, request, JsonOptions);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var device = (await created.Content.ReadFromJsonAsync<DeviceDto>())!;
        var updated = await senior.Client.PostAsJsonAsync(Devices, request with { AppVersion = "2.0.0" }, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var dto = (await updated.Content.ReadFromJsonAsync<DeviceDto>())!;
        Assert.Equal(device.DeviceId, dto.DeviceId);
        Assert.Equal(device.RegisteredAt, dto.RegisteredAt);
        Assert.Equal("2.0.0", dto.AppVersion);
        Assert.Equal(dto, Assert.Single((await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!));
        await Error(await caretaker.Client.PostAsJsonAsync(Devices, request, JsonOptions), 403, "operationForbidden");
        await Error(await caretaker.Client.PostAsJsonAsync(Devices, request with { MonitoringEnabled = false }, JsonOptions), 404, "resourceNotFound");
        var caretakerDevice = await Register(caretaker, false);
        Assert.False(caretakerDevice.MonitoringEnabled);
        await Error(await caretaker.Client.PostAsync($"{Devices}/{caretakerDevice.DeviceId}/switch", null), 403, "operationForbidden");
    }

    [PostgresFact]
    public async Task Switching_requires_explicit_confirmation_and_retiring_stops_uploads()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var first = await Register(senior);
        var secondRequest = Registration();
        await Error(await senior.Client.PostAsJsonAsync(Devices, secondRequest, JsonOptions), 409, "activeDeviceExists");
        var second = await Register(senior, secondRequest with { MonitoringEnabled = false });
        var sample = Sample(Now());
        await Error(await Upload(senior, second.DeviceId, sample), 409, "monitoringDisabled");
        var switched = await senior.Client.PostAsync($"{Devices}/{second.DeviceId}/switch", null);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var devices = (await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!;
        Assert.Equal(second.DeviceId, Assert.Single(devices, row => row.MonitoringEnabled).DeviceId);
        await Error(await Upload(senior, first.DeviceId, sample), 409, "monitoringDisabled");
        Assert.Equal(HttpStatusCode.OK, (await senior.Client.PostAsync($"{Devices}/{second.DeviceId}/switch", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Upload(senior, second.DeviceId, sample)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await senior.Client.DeleteAsync($"{Devices}/{second.DeviceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await senior.Client.DeleteAsync($"{Devices}/{second.DeviceId}")).StatusCode);
        await Error(await Upload(senior, second.DeviceId, sample), 404, "resourceNotFound");
        Assert.DoesNotContain((await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!, row => row.DeviceId == second.DeviceId);
        var reactivated = await senior.Client.PostAsJsonAsync(Devices, secondRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        Assert.Equal(second.DeviceId, (await reactivated.Content.ReadFromJsonAsync<DeviceDto>())!.DeviceId);
    }

    [PostgresFact]
    public async Task Concurrent_registrations_and_switches_never_leave_two_monitoring_devices()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var registrations = await Task.WhenAll(senior.Client.PostAsJsonAsync(Devices, Registration(), JsonOptions),
            senior.Client.PostAsJsonAsync(Devices, Registration(), JsonOptions));
        Assert.Single(registrations, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(registrations, response => response.StatusCode == HttpStatusCode.Conflict);
        var second = await Register(senior, false);
        var third = await Register(senior, false);
        var switches = await Task.WhenAll(senior.Client.PostAsync($"{Devices}/{second.DeviceId}/switch", null),
            senior.Client.PostAsync($"{Devices}/{third.DeviceId}/switch", null));
        Assert.Contains(switches, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(switches, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Single((await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!, row => row.MonitoringEnabled);
    }

    [PostgresFact]
    public async Task Telemetry_rejects_invalid_samples_individually_and_deduplicates_uploads()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        var now = Now();
        var valid = Sample(now);
        var invalid = valid with { CapturedAt = now.AddSeconds(-1), Location = new(new CoordinatesDto(91, 21), 8, 0) };
        var response = await Upload(senior, device.DeviceId, valid, valid, invalid,
            valid with { CapturedAt = now.AddSeconds(-2), Battery = new(-1, false) },
            valid with { CapturedAt = now.AddSeconds(-3), Location = new(new(52, 21), -1, null) },
            valid with { CapturedAt = now.AddSeconds(-4), Location = new(new(52, 21), null, -1) },
            valid with { CapturedAt = now.AddDays(-2) }, valid with { CapturedAt = now.AddMinutes(10) },
            new(now, null, null), null, valid with { Location = new(null, 8, 0) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batch = (await response.Content.ReadFromJsonAsync<TelemetryBatchResultDto>())!;
        Assert.Equal(new[] { "accepted", "duplicate", "rejected", "rejected", "rejected", "rejected", "rejected", "rejected", "rejected", "rejected", "rejected" },
            batch.Results.Select(row => row.Result));
        Assert.Equal("invalidCoordinates", batch.Results[2].ErrorCode);
        Assert.Equal("invalidBattery", batch.Results[3].ErrorCode);
        Assert.Equal("invalidAccuracy", batch.Results[4].ErrorCode);
        Assert.Equal("invalidSpeed", batch.Results[5].ErrorCode);
        Assert.Equal("invalidCapturedAt", batch.Results[6].ErrorCode);
        Assert.Equal("invalidCapturedAt", batch.Results[7].ErrorCode);
        Assert.Equal("emptySample", batch.Results[8].ErrorCode);
        Assert.Equal("invalidSample", batch.Results[9].ErrorCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.Equal(1, await context.LocationSamples.CountAsync(row => row.DeviceId == device.DeviceId));
        Assert.Equal(1, await context.BatterySamples.CountAsync(row => row.DeviceId == device.DeviceId));
        Assert.NotNull((await context.Devices.SingleAsync(row => row.Id == device.DeviceId)).LastOnlineAt);
        var retry = await Upload(senior, device.DeviceId, valid with { CapturedAt = now.AddTicks(7) });
        Assert.Equal("duplicate", Assert.Single((await retry.Content.ReadFromJsonAsync<TelemetryBatchResultDto>())!.Results).Result);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Location_and_battery_can_arrive_separately_without_overwriting_an_existing_measurement()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        var now = Now();
        Assert.Equal(HttpStatusCode.OK, (await Upload(senior, device.DeviceId, Sample(now) with { Battery = null })).StatusCode);
        var response = await Upload(senior, device.DeviceId, Sample(now) with { Location = null });
        Assert.Equal("accepted", Assert.Single((await response.Content.ReadFromJsonAsync<TelemetryBatchResultDto>())!.Results).Result);
        response = await Upload(senior, device.DeviceId, Sample(now) with { Location = new(new(0, 0), null, null), Battery = new(10, true) });
        Assert.Equal("duplicate", Assert.Single((await response.Content.ReadFromJsonAsync<TelemetryBatchResultDto>())!.Results).Result);
        var latest = (await senior.Client.GetFromJsonAsync<LatestLocationDto>(Location(senior.Id)))!;
        Assert.Equal(new CoordinatesDto(52.2297, 21.0122), latest.Coordinates);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.Equal(75, (await context.BatterySamples.SingleAsync(row => row.DeviceId == device.DeviceId)).BatteryLevelPercent);
    }

    [PostgresFact]
    public async Task Concurrent_retries_do_not_create_duplicate_telemetry()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var device = await Register(senior);
        var sample = Sample(Now());
        var responses = await Task.WhenAll(Upload(senior, device.DeviceId, sample), Upload(senior, device.DeviceId, sample));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var retry = await Upload(senior, device.DeviceId, sample);
        Assert.Equal("duplicate", Assert.Single((await retry.Content.ReadFromJsonAsync<TelemetryBatchResultDto>())!.Results).Result);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        Assert.Equal(1, await context.LocationSamples.CountAsync(row => row.DeviceId == device.DeviceId));
        Assert.Equal(1, await context.BatterySamples.CountAsync(row => row.DeviceId == device.DeviceId));
    }

    [PostgresFact]
    public async Task Latest_location_uses_capture_time_across_devices_and_survives_retirement()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Error(await senior.Client.GetAsync(Location(senior.Id)), 404, "resourceNotFound");
        var first = await Register(senior);
        var now = Now();
        await Upload(senior, first.DeviceId, Sample(now.AddMinutes(-5)));
        var second = await Register(senior, false);
        Assert.Equal(HttpStatusCode.OK, (await senior.Client.PostAsync($"{Devices}/{second.DeviceId}/switch", null)).StatusCode);
        await senior.Client.DeleteAsync($"{Devices}/{first.DeviceId}");
        await Upload(senior, second.DeviceId, Sample(now.AddMinutes(-10)) with { Location = new(new(0, 0), null, null) });
        var latest = (await caretaker.Client.GetFromJsonAsync<LatestLocationDto>(Location(senior.Id)))!;
        Assert.Equal(first.DeviceId, latest.DeviceId);
        Assert.Equal(now.AddMinutes(-5), latest.CapturedAt);
        Assert.Equal(new CoordinatesDto(52.2297, 21.0122), latest.Coordinates);
        Assert.Equal(latest, await senior.Client.GetFromJsonAsync<LatestLocationDto>(Location(senior.Id)));
        Assert.Equal(second.DeviceId, Assert.Single((await caretaker.Client.GetFromJsonAsync<DeviceDto[]>($"/api/v1/seniors/{senior.Id}/devices"))!).DeviceId);
        await Upload(senior, second.DeviceId, Sample(now));
        var response = await caretaker.Client.GetAsync(Location(senior.Id));
        Assert.True(response.Headers.CacheControl!.NoStore);
        latest = (await response.Content.ReadFromJsonAsync<LatestLocationDto>())!;
        Assert.Equal(second.DeviceId, latest.DeviceId);
        Assert.Equal(now, latest.CapturedAt);
        Assert.True(latest.ReceivedAt >= latest.CapturedAt);
    }

    [PostgresFact]
    public async Task Unrelated_users_cannot_read_or_mutate_a_seniors_device_or_position()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var otherSenior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var device = await Register(senior);
        await Upload(senior, device.DeviceId, Sample(Now()));
        foreach (var actor in new[] { otherSenior, caretaker })
        {
            await Error(await actor.Client.GetAsync(Location(senior.Id)), 404, "resourceNotFound");
            await Error(await actor.Client.GetAsync($"/api/v1/seniors/{senior.Id}/devices"), 404, "resourceNotFound");
            await Error(await actor.Client.DeleteAsync($"{Devices}/{device.DeviceId}"), 404, "resourceNotFound");
        }
        await Error(await otherSenior.Client.PostAsync($"{Devices}/{device.DeviceId}/switch", null), 404, "resourceNotFound");
        await Error(await Upload(otherSenior, device.DeviceId, Sample(Now())), 404, "resourceNotFound");
        await Error(await Upload(caretaker, device.DeviceId, Sample(Now())), 403, "operationForbidden");
        var relationship = await Link(senior, caretaker);
        Assert.Equal(HttpStatusCode.OK, (await caretaker.Client.GetAsync(Location(senior.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        await Error(await caretaker.Client.GetAsync(Location(senior.Id)), 404, "resourceNotFound");
        await Error(await caretaker.Client.GetAsync($"/api/v1/seniors/{senior.Id}/devices"), 404, "resourceNotFound");
    }

    [PostgresFact]
    public async Task Deactivated_seniors_are_hidden_from_their_caretakers()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var device = await Register(senior);
        await Upload(senior, device.DeviceId, Sample(Now()));
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == senior.Id);
        account.Status = AccountStatus.PendingDeletion;
        await context.SaveChangesAsync();
        await Error(await caretaker.Client.GetAsync(Location(senior.Id)), 404, "resourceNotFound");
        await Error(await caretaker.Client.GetAsync($"/api/v1/seniors/{senior.Id}/devices"), 404, "resourceNotFound");
        await Error(await Upload(senior, device.DeviceId, Sample(Now())), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Structural_validation_rejects_invalid_registration_and_invalid_batch_sizes()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var request = Registration();
        foreach (var invalid in new[] { request with { InstallationId = Guid.Empty }, request with { Platform = (DevicePlatform)99 },
            request with { AppVersion = " " }, request with { AppVersion = new string('v', 101) } })
            await Error(await senior.Client.PostAsJsonAsync(Devices, invalid, JsonOptions), 400, "validationFailed");
        var device = await Register(senior);
        await Error(await Upload(senior, device.DeviceId), 400, "validationFailed");
        await Error(await senior.Client.PostAsJsonAsync($"{Devices}/{device.DeviceId}/telemetry", new { samples = (object?)null }), 400, "validationFailed");
        await Error(await Upload(senior, device.DeviceId, Enumerable.Repeat<TelemetrySample?>(Sample(Now()), 501).ToArray()), 400, "validationFailed");
        await Error(await senior.Client.PostAsJsonAsync($"{Devices}/{device.DeviceId}/telemetry", new { samples = new[] { new { capturedAt = "not-a-date", location = (object?)null, battery = (object?)null } } }), 400, "validationFailed");
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<SafetyDbContext>().LocationSamples.AnyAsync(row => row.DeviceId == device.DeviceId));
    }

    [PostgresFact]
    public async Task Retiring_a_device_frees_a_registration_slot_and_account_cleanup_removes_telemetry()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        var first = await Register(senior);
        await Upload(senior, first.DeviceId, Sample(Now()));
        for (var index = 0; index < 9; index++) await Register(senior, false);
        await Error(await senior.Client.PostAsJsonAsync(Devices, Registration(false), JsonOptions), 409, "deviceLimitReached");
        await senior.Client.DeleteAsync($"{Devices}/{first.DeviceId}");
        await Register(senior);
        Assert.Equal(10, (await senior.Client.GetFromJsonAsync<DeviceDto[]>(Devices))!.Length);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SafetyDbContext>();
        var ids = await context.Devices.Where(row => row.AssignedAccountId == senior.Id).Select(row => row.Id).ToArrayAsync();
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>())
            await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        Assert.False(await context.Devices.AnyAsync(row => row.AssignedAccountId == senior.Id));
        Assert.False(await context.LocationSamples.AnyAsync(row => ids.Contains(row.DeviceId)));
        Assert.False(await context.BatterySamples.AnyAsync(row => ids.Contains(row.DeviceId)));
    }

    [PostgresFact]
    public async Task Anonymous_users_cannot_access_any_safety_endpoint()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var id = Guid.CreateVersion7();
        await Error(await client.GetAsync(Devices), 401, "unauthenticated");
        await Error(await client.PostAsJsonAsync(Devices, Registration(), JsonOptions), 401, "unauthenticated");
        await Error(await client.DeleteAsync($"{Devices}/{id}"), 401, "unauthenticated");
        await Error(await client.PostAsync($"{Devices}/{id}/switch", null), 401, "unauthenticated");
        await Error(await client.GetAsync($"/api/v1/seniors/{id}/devices"), 401, "unauthenticated");
        await Error(await client.GetAsync(Location(id)), 401, "unauthenticated");
        await Error(await client.PostAsJsonAsync($"{Devices}/{id}/telemetry", new UploadTelemetryRequest([Sample(Now())])), 401, "unauthenticated");
    }

    private async Task<TestActor> Actor(WebApplicationFactory<Program> factory, AccountRole role)
    {
        var phone = "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        await using var context = fixture.Context();
        var now = DateTimeOffset.UtcNow;
        var account = new Account
        {
            RoleId = await context.Roles.Where(row => row.Code == role).Select(row => row.Id).SingleAsync(),
            PhoneNumber = phone, NormalizedPhoneNumber = phone, PasswordHash = "", FirstName = "Jan", LastName = "Kowalski",
            Status = AccountStatus.Active, PhoneVerifiedAt = now, CreatedAt = now, UpdatedAt = now
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
        var created = await caretaker.Client.PostAsync("/api/v1/care-invitations", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invitation = (await created.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }

    private static string Location(Guid seniorId) => $"/api/v1/seniors/{seniorId}/locations/latest";
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
