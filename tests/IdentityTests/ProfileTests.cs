using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.SeniorProfile.Application.Accessibility;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class ProfileTests(IdentityApiFixture fixture)
{
    private const string Password = "TestPass1";

    [PostgresTheory]
    [InlineData(AccountRole.Senior)]
    [InlineData(AccountRole.Caretaker)]
    public async Task Profile_edit_updates_only_own_names_and_requires_current_version(AccountRole role)
    {
        await using var factory = fixture.Factory();
        using var actor = await Actor(factory, role);
        using var other = await Actor(factory, role);
        var beforeResponse = await actor.Client.GetAsync("/api/v1/me");
        var before = (await beforeResponse.Content.ReadFromJsonAsync<AccountDto>())!;
        var etag = beforeResponse.Headers.ETag!.Tag;
        var body = new { firstName = "  Anna  ", lastName = "  Nowak  " };
        await Error(await Put(actor.Client, "/api/v1/me/profile", body), 428, "preconditionRequired");
        await Error(await Put(actor.Client, "/api/v1/me/profile", body, "1"), 400, "validationFailed");
        await Error(await Put(actor.Client, "/api/v1/me/profile", new { firstName = " ", lastName = "Nowak" }, etag), 400, "validationFailed");
        await Error(await Put(actor.Client, "/api/v1/me/profile", new { firstName = new string('a', 101), lastName = "Nowak" }, etag), 400, "validationFailed");
        await Error(await Put(actor.Client, "/api/v1/me/profile", new { firstName = "Anna", lastName = "Nowak", email = "other@example.test" }, etag), 400, "validationFailed");
        var saved = await Put(actor.Client, "/api/v1/me/profile", body, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var after = (await saved.Content.ReadFromJsonAsync<AccountDto>())!;
        Assert.Equal("Anna", after.FirstName);
        Assert.Equal("Nowak", after.LastName);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal($"\"{after.Version}\"", saved.Headers.ETag!.Tag);
        Assert.Equal(before with { FirstName = "Anna", LastName = "Nowak", Version = after.Version }, after);
        await Error(await Put(actor.Client, "/api/v1/me/profile", body, etag), 412, "versionMismatch");
        var own = (await actor.Client.GetFromJsonAsync<AccountDto>("/api/v1/me"))!;
        Assert.Equal(after, own);
        Assert.Equal("Kowalski", (await other.Client.GetFromJsonAsync<AccountDto>("/api/v1/me"))!.LastName);
    }

    [PostgresFact]
    public async Task Concurrent_profile_updates_allow_only_one_write_for_a_version()
    {
        await using var factory = fixture.Factory();
        using var actor = await Actor(factory, AccountRole.Senior);
        var before = await actor.Client.GetAsync("/api/v1/me");
        var etag = before.Headers.ETag!.Tag;
        var writes = await Task.WhenAll(
            Put(actor.Client, "/api/v1/me/profile", new { firstName = "Anna", lastName = "First" }, etag),
            Put(actor.Client, "/api/v1/me/profile", new { firstName = "Piotr", lastName = "Second" }, etag));
        var success = Assert.Single(writes, response => response.StatusCode == HttpStatusCode.OK);
        await Error(Assert.Single(writes, response => response.StatusCode == HttpStatusCode.PreconditionFailed), 412, "versionMismatch");
        Assert.Equal(await success.Content.ReadFromJsonAsync<AccountDto>(), await actor.Client.GetFromJsonAsync<AccountDto>("/api/v1/me"));
    }

    [PostgresFact]
    public async Task Concurrent_first_reads_initialize_one_default_record_and_cleanup_removes_it()
    {
        await using var factory = fixture.Factory();
        using var actor = await Actor(factory, AccountRole.Senior);
        var path = Preferences(actor.Id);
        var responses = await Task.WhenAll(actor.Client.GetAsync(path), actor.Client.GetAsync(path));
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            var preferences = (await response.Content.ReadFromJsonAsync<AccessibilityPreferencesDto>())!;
            Assert.Equal(actor.Id, preferences.SeniorId);
            Assert.Equal(100, preferences.FontScalePercent);
            Assert.False(preferences.HighContrastEnabled);
            Assert.False(preferences.LargeButtonsEnabled);
            Assert.Equal(1, preferences.Version);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SeniorProfileDbContext>();
        Assert.Equal(1, await context.AccessibilityPreferences.CountAsync(row => row.SeniorId == actor.Id));
        Assert.False(context.Database.HasPendingModelChanges());
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>())
            await cleanup.DeleteAsync(actor.Id, CancellationToken.None);
        Assert.False(await context.AccessibilityPreferences.AnyAsync(row => row.SeniorId == actor.Id));
    }

    [PostgresFact]
    public async Task Preferences_require_strong_if_match_and_preserve_data_on_invalid_requests()
    {
        await using var factory = fixture.Factory();
        using var actor = await Actor(factory, AccountRole.Senior);
        var path = Preferences(actor.Id);
        var initial = await actor.Client.GetAsync(path);
        var etag = initial.Headers.ETag!.Tag;
        var body = Settings(200);
        await Error(await Put(actor.Client, path, body), 428, "preconditionRequired");
        foreach (var invalid in new[] { "1", "\"0\"", "W/\"1\"", "*", "\"1\", \"2\"", "\"2147483648\"" })
            await Error(await Put(actor.Client, path, body, invalid), 400, "validationFailed");
        await Error(await Put(actor.Client, path, Settings(99), etag), 400, "validationFailed");
        await Error(await Put(actor.Client, path, Settings(401), etag), 400, "validationFailed");
        await Error(await Put(actor.Client, path, new { fontScalePercent = 200, highContrastEnabled = true }, etag), 400, "validationFailed");
        var savedResponse = await Put(actor.Client, path, body, etag);
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<AccessibilityPreferencesDto>())!;
        Assert.Equal(200, saved.FontScalePercent);
        Assert.True(saved.HighContrastEnabled);
        Assert.True(saved.LargeButtonsEnabled);
        Assert.Equal(2, saved.Version);
        Assert.Equal("\"2\"", savedResponse.Headers.ETag!.Tag);
        await Error(await Put(actor.Client, path, Settings(300), etag), 412, "versionMismatch");
        Assert.Equal(saved, await actor.Client.GetFromJsonAsync<AccessibilityPreferencesDto>(path));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SeniorProfileDbContext>();
        Assert.Equal(actor.Id, (await context.AccessibilityPreferences.SingleAsync(row => row.SeniorId == actor.Id)).UpdatedByAccountId);
    }

    [PostgresFact]
    public async Task Direct_first_put_initializes_and_saves_preferences()
    {
        await using var factory = fixture.Factory();
        using var actor = await Actor(factory, AccountRole.Senior);
        var response = await Put(actor.Client, Preferences(actor.Id), Settings(400), "\"1\"");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<AccessibilityPreferencesDto>())!;
        Assert.Equal(400, saved.FontScalePercent);
        Assert.Equal(2, saved.Version);
    }

    [PostgresFact]
    public async Task Concurrent_preferences_updates_allow_only_one_write_for_a_version()
    {
        await using var factory = fixture.Factory();
        using var actor = await Actor(factory, AccountRole.Senior);
        var path = Preferences(actor.Id);
        var first = await actor.Client.GetAsync(path);
        var etag = first.Headers.ETag!.Tag;
        var responses = await Task.WhenAll(Put(actor.Client, path, Settings(150), etag), Put(actor.Client, path, Settings(300), etag));
        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        await Error(Assert.Single(responses, response => response.StatusCode == HttpStatusCode.PreconditionFailed), 412, "versionMismatch");
        Assert.Equal(await success.Content.ReadFromJsonAsync<AccessibilityPreferencesDto>(),
            await actor.Client.GetFromJsonAsync<AccessibilityPreferencesDto>(path));
    }

    [PostgresFact]
    public async Task Caretaker_access_requires_current_relationship_and_active_senior()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var otherSenior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var path = Preferences(senior.Id);
        await Error(await caretaker.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await Put(caretaker.Client, path, Settings(200), "\"1\""), 404, "resourceNotFound");
        await Error(await caretaker.Client.GetAsync(Preferences(caretaker.Id)), 404, "resourceNotFound");
        await Error(await otherSenior.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await senior.Client.GetAsync(Preferences(Guid.CreateVersion7())), 404, "resourceNotFound");
        var invitationResponse = await caretaker.Client.PostAsync("/api/v1/care-invitations", null);
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitation = (await invitationResponse.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var relationship = (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
        var preferences = await caretaker.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, preferences.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(caretaker.Client, path, Settings(250), preferences.Headers.ETag!.Tag)).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SeniorProfileDbContext>();
            Assert.Equal(caretaker.Id, (await context.AccessibilityPreferences.SingleAsync(row => row.SeniorId == senior.Id)).UpdatedByAccountId);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        await Error(await caretaker.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await Put(caretaker.Client, path, Settings(300), "\"2\""), 404, "resourceNotFound");
        Assert.Equal(250, (await senior.Client.GetFromJsonAsync<AccessibilityPreferencesDto>(path))!.FontScalePercent);
        await using var identity = fixture.Context();
        var account = await identity.Accounts.SingleAsync(row => row.Id == senior.Id);
        account.Status = AccountStatus.PendingDeletion;
        account.SecurityStamp = Guid.CreateVersion7();
        await identity.SaveChangesAsync();
        await Error(await senior.Client.GetAsync(path), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Anonymous_requests_cannot_read_or_modify_profiles()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        await Error(await Put(client, "/api/v1/me/profile", new { firstName = "Anna", lastName = "Nowak" }, "\"1\""), 401, "unauthenticated");
        var path = Preferences(Guid.CreateVersion7());
        await Error(await client.GetAsync(path), 401, "unauthenticated");
        await Error(await Put(client, path, Settings(200), "\"1\""), 401, "unauthenticated");
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
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new TestActor(account.Id, phone, client);
    }

    private static string Preferences(Guid seniorId) => $"/api/v1/seniors/{seniorId}/accessibility-preferences";
    private static object Settings(int scale) => new { fontScalePercent = scale, highContrastEnabled = true, largeButtonsEnabled = true };

    private static Task<HttpResponseMessage> Put(HttpClient client, string path, object body, string? etag = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return client.SendAsync(request);
    }

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
