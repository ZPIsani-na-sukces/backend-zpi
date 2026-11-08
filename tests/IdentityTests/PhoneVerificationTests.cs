using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Application.Accounts.UnverifiedAccountCleanup;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Sms;

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

    [PostgresTheory]
    [InlineData(PhoneCodeDelivery.InvalidPhoneNumber, HttpStatusCode.BadRequest, "validationFailed")]
    [InlineData(PhoneCodeDelivery.Unavailable, HttpStatusCode.ServiceUnavailable, "deliveryUnavailable")]
    public async Task Registration_whose_sms_fails_is_not_saved_and_can_be_retried(PhoneCodeDelivery delivery,
        HttpStatusCode status, string code)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var phone = Phone();
        var email = Guid.CreateVersion7() + "@example.com";
        var request = new { role = "caretaker", firstName = "Anna", lastName = "Nowak", phoneNumber = phone, email, password = Password };
        sender.Respond(phone, delivery);

        var failed = await client.PostAsJsonAsync("/api/v1/auth/registrations", request);
        Assert.Equal(status, failed.StatusCode);
        using (var body = await JsonDocument.ParseAsync(await failed.Content.ReadAsStreamAsync()))
        {
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
            var hasPhoneError = body.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object && errors.TryGetProperty("phoneNumber", out _);
            Assert.Equal(delivery == PhoneCodeDelivery.InvalidPhoneNumber, hasPhoneError);
        }
        await using (var context = fixture.Context())
        {
            Assert.False(await context.Accounts.AnyAsync(row =>
                row.NormalizedPhoneNumber == phone || row.NormalizedEmail == email.ToUpperInvariant()));
            Assert.False(await context.VerificationChallenges.AnyAsync(row => row.Destination == phone));
        }

        sender.Respond(phone, PhoneCodeDelivery.Sent);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/registrations", request)).StatusCode);
    }

    [PostgresFact]
    public async Task Resend_whose_sms_fails_keeps_the_previous_code_valid()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var phone = Phone();
        var registrationResponse = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        var registration = (await registrationResponse.Content.ReadFromJsonAsync<RegistrationDto>())!;
        var challengeId = registration.PhoneChallenge.ChallengeId;
        await using (var context = fixture.Context())
        {
            var challenge = await context.VerificationChallenges.SingleAsync(row => row.Id == challengeId);
            challenge.ResendAfter = DateTimeOffset.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync();
        }
        sender.Respond(phone, PhoneCodeDelivery.Unavailable);

        var resend = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications",
            new { verificationTicket = registration.VerificationTicket });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resend.StatusCode);
        await using (var context = fixture.Context())
        {
            var remaining = Assert.Single(await context.VerificationChallenges
                .Where(row => row.AccountId == registration.UserId).ToListAsync());
            Assert.Equal(challengeId, remaining.Id);
            Assert.Null(remaining.InvalidatedAt);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId, code = sender.CodeFor(challengeId) })).StatusCode);
    }

    [PostgresFact]
    public async Task Cleanup_deletes_only_accounts_left_unverified_for_a_day_and_frees_their_phone_number()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        object Registration(string phone) =>
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password };
        async Task<RegistrationDto> Register(string phone) =>
            (await (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(phone))).Content.ReadFromJsonAsync<RegistrationDto>())!;
        var stalePhone = Phone();
        var stale = await Register(stalePhone);
        var fresh = await Register(Phone());
        var verified = await Register(Phone());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = verified.PhoneChallenge.ChallengeId, code = sender.CodeFor(verified.PhoneChallenge.ChallengeId) })).StatusCode);
        var longAgo = DateTimeOffset.UtcNow - UnverifiedAccountCleanupProcessor.MaximumAge - TimeSpan.FromMinutes(1);
        await using (var context = fixture.Context())
            await context.Accounts.Where(row => row.Id == stale.UserId || row.Id == verified.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.CreatedAt, longAgo));

        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<UnverifiedAccountCleanupProcessor>().ProcessAsync(CancellationToken.None);

        await using (var context = fixture.Context())
        {
            Assert.False(await context.Accounts.AnyAsync(row => row.Id == stale.UserId));
            Assert.False(await context.VerificationTickets.AnyAsync(row => row.AccountId == stale.UserId));
            Assert.False(await context.VerificationChallenges.AnyAsync(row => row.AccountId == stale.UserId));
            Assert.True(await context.Accounts.AnyAsync(row => row.Id == fresh.UserId));
            Assert.True(await context.Accounts.AnyAsync(row => row.Id == verified.UserId));
        }
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/registrations", Registration(stalePhone))).StatusCode);
    }

    [PostgresFact]
    public async Task Fixed_code_verifies_the_phone_but_is_never_used_for_password_reset()
    {
        var phone = Phone();
        await using var baseFactory = fixture.Factory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPhoneVerificationCodes>(new AlwaysFixedCodes())));
        using var client = factory.CreateClient();
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();

        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role = "senior", firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email = (string?)null, password = Password });
        var registration = (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
        Assert.Equal(SmsRecipientAllowlist.FixedCode, sender.CodeFor(registration.PhoneChallenge.ChallengeId));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = SmsRecipientAllowlist.FixedCode })).StatusCode);

        var reset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        var challenge = (await reset.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.Matches(@"\A[0-9]{6}\z", sender.CodeFor(challenge.ChallengeId));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
            new { challengeId = challenge.ChallengeId, code = SmsRecipientAllowlist.FixedCode })).StatusCode);
    }

    private sealed class AlwaysFixedCodes : IPhoneVerificationCodes
    {
        public string? FixedCodeFor(string phoneNumber) => SmsRecipientAllowlist.FixedCode;
    }
}
