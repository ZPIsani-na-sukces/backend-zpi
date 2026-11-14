using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;
using StackExchange.Redis;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class AccountLifecycleTests(IdentityApiFixture fixture)
{
    private const string Password = "A long password 123!";

    [PostgresFact]
    public async Task Logout_removes_session_refresh_token_but_keeps_other_session()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (id, phone) = await ActiveAccount(client);
        var first = await Login(client, phone);
        var second = await Login(client, phone);
        var sessionId = Id(first);
        Bearer(client, first);
        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        AssertRefreshCookieCleared(logout);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, first.RefreshToken!)).StatusCode);
        var store = factory.Services.GetRequiredService<ISessionStore>();
        Assert.Null(await store.GetAsync(sessionId, CancellationToken.None));
        Assert.Null(await store.FindByRefreshTokenAsync(TokenService.HashSecret(first.RefreshToken!), CancellationToken.None));
        Bearer(client, second);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Refresh_rotates_token_and_reusing_old_token_revokes_the_session()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        var first = await Login(client, phone);
        var response = await Refresh(client, first.RefreshToken!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
        Assert.Equal(Id(first), Id(rotated));
        Bearer(client, rotated);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, first.RefreshToken!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, rotated.RefreshToken!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_refresh_cannot_use_the_same_token_twice()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        var session = await Login(client, phone);
        var responses = await Task.WhenAll(Refresh(client, session.RefreshToken!), Refresh(client, session.RefreshToken!));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        Bearer(client, session);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [PostgresFact]
    public async Task Web_refresh_uses_cookie_rotates_it_and_checks_origin()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        var (_, phone) = await ActiveAccount(client);
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "web" });
        var originalCookie = Assert.Single(login.Headers.GetValues("Set-Cookie")).Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", originalCookie);
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { clientType = "web" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<SessionDto>())!.RefreshToken);
        var rotatedCookie = Assert.Single(response.Headers.GetValues("Set-Cookie")).Split(';')[0];
        Assert.NotEqual(originalCookie, rotatedCookie);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", rotatedCookie);
        client.DefaultRequestHeaders.Remove("Origin");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { clientType = "web" })).StatusCode);
    }

    [PostgresFact]
    public async Task Changing_password_requires_current_password_and_invalidates_all_sessions()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (_, phone) = await ActiveAccount(client);
        var first = await Login(client, phone);
        var second = await Login(client, phone);
        Bearer(client, first);
        const string newPassword = "NewPass2";
        var store = factory.Services.GetRequiredService<ISessionStore>();
        var staleSession = (await store.GetAsync(Id(first), CancellationToken.None))!;
        var rejected = await client.PutAsJsonAsync("/api/v1/auth/password", new { currentPassword = "wrong", newPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        var changed = await client.PutAsJsonAsync("/api/v1/auth/password", new { currentPassword = Password, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        AssertRefreshCookieCleared(changed);
        await store.SaveAsync(staleSession, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, first.RefreshToken!)).StatusCode);
        Bearer(client, second);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, second.RefreshToken!)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = newPassword, clientType = "mobile" })).StatusCode);
    }

    [PostgresFact]
    public async Task Deletion_requires_password_disables_account_and_schedules_removal_in_30_days()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (id, phone) = await ActiveAccount(client);
        var first = await Login(client, phone);
        var second = await Login(client, phone);
        Bearer(client, first);
        var rejected = await Delete(client, "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
        var response = await Delete(client, Password);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        AssertRefreshCookieCleared(response);
        var body = (await response.Content.ReadFromJsonAsync<AccountDeletionDto>())!;
        Assert.InRange((body.DeletionScheduledAt - DateTimeOffset.UtcNow).TotalDays, 29.99, 30);
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == id);
        Assert.Equal(AccountStatus.PendingDeletion, account.Status);
        Assert.NotNull(account.DeletionRequestedAt);
        Bearer(client, second);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, second.RefreshToken!)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" })).StatusCode);
    }

    [PostgresFact]
    public async Task Deletion_job_removes_due_account_and_owned_data_but_keeps_recent_and_active_accounts()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var (dueId, _) = await ActiveAccount(client);
        var (recentId, _) = await ActiveAccount(client);
        var (activeId, _) = await ActiveAccount(client);
        await using (var context = fixture.Context())
        {
            foreach (var (id, days) in new[] { (dueId, 31), (recentId, 29) })
            {
                var row = await context.Accounts.SingleAsync(row => row.Id == id);
                row.Status = AccountStatus.PendingDeletion;
                row.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-days);
            }
            await context.SaveChangesAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var profile = scope.ServiceProvider.GetRequiredService<SeniorProfileDbContext>();
        foreach (var id in new[] { dueId, activeId })
            profile.SeniorContactPersons.Add(new SeniorContactPerson
            {
                SeniorId = id,
                DisplayName = "Contact",
                PhoneNumber = "+48123456789",
                CreatedByAccountId = id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await profile.SaveChangesAsync();
        var processor = scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>();
        await processor.ProcessAsync(CancellationToken.None);
        await processor.ProcessAsync(CancellationToken.None);
        await using var verify = fixture.Context();
        Assert.False(await verify.Accounts.AnyAsync(row => row.Id == dueId));
        Assert.True(await verify.Accounts.AnyAsync(row => row.Id == recentId));
        Assert.True(await verify.Accounts.AnyAsync(row => row.Id == activeId));
        Assert.False(await verify.VerificationTickets.AnyAsync(row => row.AccountId == dueId));
        Assert.False(await verify.VerificationChallenges.AnyAsync(row => row.AccountId == dueId));
        Assert.False(await profile.SeniorContactPersons.AnyAsync(row => row.SeniorId == dueId));
        Assert.True(await profile.SeniorContactPersons.AnyAsync(row => row.SeniorId == activeId));
    }

    private static void AssertRefreshCookieCleared(HttpResponseMessage response)
    {
        var cookie = SetCookieHeaderValue.Parse(Assert.Single(response.Headers.GetValues("Set-Cookie")));
        Assert.Equal("zpi_refresh_token", cookie.Name.Value);
        Assert.Equal("", cookie.Value.Value);
        Assert.Equal("/api/v1/auth", cookie.Path.Value);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.NotNull(cookie.Expires);
        Assert.True(cookie.Expires.Value < DateTimeOffset.UtcNow);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    private async Task<(Guid Id, string Phone)> ActiveAccount(HttpClient client)
    {
        var phone = "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations", new
        { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<RegistrationDto>())!.UserId;
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == id);
        account.Status = AccountStatus.Active;
        account.PhoneVerifiedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        return (id, phone);
    }

    private static async Task<SessionDto> Login(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { clientType = "mobile", refreshToken = token });

    private static Task<HttpResponseMessage> Delete(HttpClient client, string password) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me") { Content = JsonContent.Create(new { password }) });

    private static Guid Id(SessionDto session) => Guid.Parse(new JsonWebToken(session.AccessToken).GetClaim("sid").Value);
    private static void Bearer(HttpClient client, SessionDto session) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
}
