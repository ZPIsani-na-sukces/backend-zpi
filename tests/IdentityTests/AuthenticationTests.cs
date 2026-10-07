using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using StackExchange.Redis;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class AuthenticationTests(IdentityApiFixture fixture)
{
    private const string Password = "A long password 123!";
    private static string Phone() => "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
    private static object Registration(string phone, string role = "senior", string? email = null, string password = Password) =>
        new { role, firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email, password };

    [PostgresTheory]
    [InlineData("senior")]
    [InlineData("caretaker")]
    public async Task Registration_persists_pending_account_with_hashed_credentials(string role)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = role == "caretaker" ? Guid.CreateVersion7() + "@example.com" : null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(phone, role, email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
        Assert.Equal("pendingPhoneVerification", body.Status);
        Assert.Equal('7', body.UserId.ToString()[14]);
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(account => account.Id == body.UserId);
        Assert.NotEqual(Password, account.PasswordHash);
        Assert.Equal(AccountStatus.PendingPhoneVerification, account.Status);
        Assert.Null(account.PhoneVerifiedAt);
        var ticket = await context.VerificationTickets.SingleAsync(ticket => ticket.AccountId == account.Id);
        Assert.Equal(TokenService.HashSecret(body.VerificationTicket), ticket.TokenHash);
        var challenge = await context.VerificationChallenges.SingleAsync(challenge => challenge.Id == body.PhoneChallenge.ChallengeId);
        Assert.Equal(ticket.Id, challenge.TicketId);
        Assert.Equal(phone, challenge.Destination);
    }

    [PostgresTheory]
    [InlineData("senior")]
    [InlineData("caretaker")]
    public async Task Active_account_can_login_and_use_JWT_on_me(string role)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = Guid.CreateVersion7() + "@example.com";
        var id = await RegisterAndActivate(client, phone, role, email);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = role == "senior" ? phone : email.ToUpperInvariant(), password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal(role, session.User.Role);
        Assert.True(session.User.PhoneVerified);
        Assert.NotNull(session.RefreshToken);
        var json = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", json);
        Assert.DoesNotContain(Password, json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(id, (await me.Content.ReadFromJsonAsync<AccountDto>())!.UserId);
        var savedSession = await factory.Services.GetRequiredService<ISessionStore>().GetAsync(SessionId(session), CancellationToken.None);
        Assert.NotNull(savedSession);
        Assert.Equal(id, savedSession.AccountId);
        Assert.Equal(TokenService.HashSecret(session.RefreshToken!), savedSession.RefreshTokenHash);
        Assert.NotEqual(session.RefreshToken, savedSession.RefreshTokenHash);
        var redis = factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var ttl = await redis.KeyTimeToLiveAsync($"{fixture.RedisPrefix}session:{savedSession.Id:N}");
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value.TotalDays, 29, 30);

    }

    [PostgresFact]
    public async Task Pending_account_gets_ticket_only_after_correct_password()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(phone));
        var bad = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = "Wrong password", clientType = "mobile" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.DoesNotContain("verificationTicket", await bad.Content.ReadAsStringAsync());
        var good = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.Forbidden, good.StatusCode);
        using var json = JsonDocument.Parse(await good.Content.ReadAsStringAsync());
        Assert.Equal("phoneVerificationRequired", json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("verificationTicket").GetString()));
    }

    [PostgresFact]
    public async Task Duplicate_phone_and_normalized_email_are_rejected()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = Guid.CreateVersion7() + "@example.com";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(phone, email: email))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(phone))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(Phone(), email: email.ToUpperInvariant()))).StatusCode);
    }

    [PostgresFact]
    public async Task Caretaker_requires_email_and_invalid_phone_is_rejected()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(Phone(), "caretaker"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration("123"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(Phone(), password: "short"))).StatusCode);
    }

    [PostgresFact]
    public async Task Web_refresh_secret_is_in_secure_cookie_and_not_body()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var phone = Phone();
        await RegisterAndActivate(client, phone);
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "web" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<SessionDto>())!.RefreshToken);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Contains("path=/api/v1/auth", cookie.ToLowerInvariant());
    }

    [PostgresFact]
    public async Task Blocked_account_and_revoked_session_cannot_access_data()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var id = await RegisterAndActivate(client, phone);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        await factory.Services.GetRequiredService<ISessionStore>().RevokeAsync(SessionId(session), CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        await using (var context = fixture.Context())
        {
            var row = await context.Accounts.SingleAsync(row => row.Id == id);
            row.Status = AccountStatus.Blocked;
            await context.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" })).StatusCode);
    }

    [PostgresFact]
    public async Task Anonymous_request_to_me_is_rejected()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [PostgresFact]
    public async Task Unknown_identifier_and_wrong_password_have_the_same_response()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        await RegisterAndActivate(client, phone);
        foreach (var identifier in new[] { phone, Phone() })
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password = "Wrong password", clientType = "mobile" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("invalidCredentials", body.RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("verificationTicket", body.RootElement.GetRawText());
        }
    }

    [PostgresFact]
    public async Task Tampered_JWT_and_cross_origin_web_login_are_rejected()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        await RegisterAndActivate(client, phone);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        var token = (await response.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken;
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'a' ? "b" : "a") + parts[2][1..];
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "web" })).StatusCode);
    }

    [PostgresFact]
    public async Task Missing_or_numeric_role_does_not_default_to_senior()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var input = new { role = 0, firstName = "Jan", lastName = "Kowalski", phoneNumber = Phone(), email = (string?)null, password = Password };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/registrations", input)).StatusCode);
        var withoutRole = new { firstName = "Jan", lastName = "Kowalski", phoneNumber = Phone(), email = (string?)null, password = Password };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/registrations", withoutRole)).StatusCode);
    }

    [PostgresFact]
    public async Task Excess_auth_requests_return_429_with_retry_after()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < 10; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/login", new { })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [PostgresFact]
    public async Task Expired_redis_session_rejects_otherwise_valid_JWT()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        await RegisterAndActivate(client, phone);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var redis = factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var key = $"{fixture.RedisPrefix}session:{SessionId(session):N}";
        await redis.KeyExpireAsync(key, TimeSpan.FromMilliseconds(100));
        await Task.Delay(200);
        Assert.False(await redis.KeyExistsAsync(key));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    private static Guid SessionId(SessionDto session) => Guid.Parse(new JsonWebToken(session.AccessToken).GetClaim("sid").Value);

    [PostgresFact]
    public async Task Invalid_registration_returns_field_errors_without_creating_account()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations", new
        {
            role = "caretaker",
            firstName = " ",
            lastName = "",
            phoneNumber = phone,
            email = "invalid-email",
            password = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validationFailed", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        foreach (var field in new[] { "firstName", "lastName", "email", "password" })
            Assert.True(errors.TryGetProperty(field, out _));
        await using var context = fixture.Context();
        Assert.False(await context.Accounts.AnyAsync(account => account.NormalizedPhoneNumber == phone));
    }

    [PostgresFact]
    public async Task Invalid_auth_requests_do_not_rotate_tokens_change_password_or_delete_account()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        await RegisterAndActivate(client, phone);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = phone, password = Password, clientType = "mobile" });
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { clientType = "mobile" })).StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { clientType = "web", refreshToken = session.RefreshToken })).StatusCode);
        var change = await client.PutAsJsonAsync("/api/v1/auth/password", new { currentPassword = Password, newPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        using var body = JsonDocument.Parse(await change.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("newPassword", out _));
        var deletion = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me") { Content = JsonContent.Create(new { password = "" }) });
        Assert.Equal(HttpStatusCode.BadRequest, deletion.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { clientType = "mobile", refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    private async Task<Guid> RegisterAndActivate(HttpClient client, string phone, string role = "senior", string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(phone, role, email));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<RegistrationDto>())!.UserId;
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(account => account.Id == id);
        account.Status = AccountStatus.Active;
        account.PhoneVerifiedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();
        return id;
    }
}
