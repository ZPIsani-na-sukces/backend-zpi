using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class PhoneVerificationTests(IdentityApiFixture fixture)
{
    private const string Password = "A long password 123!";
    private static string Phone() => "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);

    [PostgresFact]
    public async Task Registration_code_confirms_phone_once_and_enables_login()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registration = (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
        var code = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Matches(@"\A[0-9]{6}\z", code);
        var loginRequest = new { identifier = phone, password = Password, clientType = "mobile" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest)).StatusCode);

        var confirm = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code })).StatusCode);
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == registration.UserId);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.NotNull(account.PhoneVerifiedAt);
        Assert.NotNull((await context.VerificationTickets.SingleAsync(row =>
            row.TokenHash == TokenService.HashSecret(registration.VerificationTicket))).ConsumedAt);
    }

    [PostgresFact]
    public async Task Resend_waits_60_seconds_and_invalidates_previous_code()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registrationResponse = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegistrationDto>())!;
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var oldCode = sender.CodeFor(registration.PhoneChallenge.ChallengeId);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        using var loginBody = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var ticket = loginBody.RootElement.GetProperty("verificationTicket").GetString()!;
        var request = new { verificationTicket = ticket };
        var cooldown = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, cooldown.StatusCode);
        Assert.InRange(cooldown.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        await using (var context = fixture.Context())
        {
            Assert.Equal(1, await context.VerificationChallenges.CountAsync(row => row.AccountId == registration.UserId));
            var challenge = await context.VerificationChallenges.SingleAsync(row => row.Id == registration.PhoneChallenge.ChallengeId);
            challenge.ResendAfter = DateTimeOffset.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync();
        }
        var resend = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications", request);
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        var next = (await resend.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.NotEqual(registration.PhoneChallenge.ChallengeId, next.ChallengeId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = oldCode })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = next.ChallengeId, code = sender.CodeFor(next.ChallengeId) })).StatusCode);
    }

    [PostgresFact]
    public async Task Login_returns_active_registration_challenge_without_sending_new_code()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registrationResponse = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegistrationDto>())!;
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        using var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var challenge = body.RootElement.GetProperty("phoneChallenge").Deserialize<ChallengeDto>(JsonSerializerOptions.Web)!;
        Assert.Equal(registration.PhoneChallenge, challenge);
        await using (var context = fixture.Context())
            Assert.Equal(1, await context.VerificationChallenges.CountAsync(row => row.AccountId == registration.UserId));

        var code = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = challenge.ChallengeId, code })).StatusCode);
    }

    [PostgresTheory]
    [InlineData("expired")]
    [InlineData("locked")]
    [InlineData("ticketExpired")]
    public async Task Login_omits_challenge_that_can_no_longer_be_confirmed(string state)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registrationResponse = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegistrationDto>())!;
        await using (var context = fixture.Context())
        {
            var challenge = await context.VerificationChallenges.SingleAsync(row => row.Id == registration.PhoneChallenge.ChallengeId);
            var ticket = await context.VerificationTickets.SingleAsync(row => row.Id == challenge.TicketId);
            switch (state)
            {
                case "expired":
                    challenge.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6);
                    challenge.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                    break;
                case "locked":
                    challenge.Attempts = 5;
                    challenge.InvalidatedAt = DateTimeOffset.UtcNow;
                    break;
                case "ticketExpired":
                    ticket.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-31);
                    ticket.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                    break;
            }
            await context.SaveChangesAsync();
        }
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = Password, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        using var body = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("verificationTicket").GetString()));
        Assert.False(body.RootElement.TryGetProperty("phoneChallenge", out _));
    }

    [PostgresFact]
    public async Task Concurrent_wrong_codes_cannot_exceed_the_attempt_limit_or_unlock_the_challenge()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = Phone(), email = (string?)null, password = Password });
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegistrationDto>())!;
        var challengeId = registration.PhoneChallenge.ChallengeId;
        var code = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challengeId);
        var wrongCode = code == "000000" ? "000001" : "000000";
        await using (var context = fixture.Context())
        {
            var challenge = await context.VerificationChallenges.SingleAsync(row => row.Id == challengeId);
            challenge.Attempts = 4;
            await context.SaveChangesAsync();
        }
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm", new { challengeId, code = wrongCode }),
            client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm", new { challengeId, code = wrongCode }));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId, code })).StatusCode);
        await using var verify = fixture.Context();
        var stored = await verify.VerificationChallenges.SingleAsync(row => row.Id == challengeId);
        Assert.Equal(5, stored.Attempts);
        Assert.NotNull(stored.InvalidatedAt);
        Assert.Null(stored.ConsumedAt);
        Assert.Equal(AccountStatus.PendingPhoneVerification,
            (await verify.Accounts.SingleAsync(row => row.Id == registration.UserId)).Status);
    }

    [PostgresFact]
    public async Task Five_wrong_codes_lock_the_challenge()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var registrationResponse = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = Phone(), email = (string?)null, password = Password });
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegistrationDto>())!;
        var realCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        var wrongCode = realCode == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
                new { challengeId = registration.PhoneChallenge.ChallengeId, code = wrongCode })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = realCode })).StatusCode);
        await using var context = fixture.Context();
        var challenge = await context.VerificationChallenges.SingleAsync(row => row.Id == registration.PhoneChallenge.ChallengeId);
        Assert.Equal(5, challenge.Attempts);
        Assert.NotNull(challenge.InvalidatedAt);
        Assert.Equal(AccountStatus.PendingPhoneVerification,
            (await context.Accounts.SingleAsync(row => row.Id == registration.UserId)).Status);
    }
}
