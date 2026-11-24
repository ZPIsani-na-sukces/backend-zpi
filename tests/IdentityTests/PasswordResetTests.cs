using Zpi.Modules.Identity.Contracts;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Application.Accounts.VerificationStatus;
using Zpi.Modules.Identity.Domain;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class PasswordResetTests(IdentityApiFixture fixture)
{
    private const string OldPassword = "OldPass1";
    private const string NewPassword = "NewPass2";
    private static string Phone() => "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);

    [PostgresFact]
    public async Task Verification_rejects_phone_confirmation_codes_unknown_and_expired_challenges()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var phoneCode = sender.CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
            new { challengeId = Guid.CreateVersion7(), code = phoneCode })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var start = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        var challenge = (await start.Content.ReadFromJsonAsync<ChallengeDto>())!;
        await using (var context = fixture.Context())
        {
            var stored = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            stored.CreatedAt = stored.ExpiresAt.AddMinutes(-5);
            stored.ResendAfter = stored.CreatedAt.AddSeconds(60);
            await context.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
            new { challengeId = challenge.ChallengeId, code = sender.CodeFor(challenge.ChallengeId) })).StatusCode);
    }

    [PostgresFact]
    public async Task Code_verification_does_not_consume_code_and_reset_still_requires_correct_code()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = sender.CodeFor(registration.PhoneChallenge.ChallengeId) })).StatusCode);
        var start = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        var challenge = (await start.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = sender.CodeFor(challenge.ChallengeId);
        var body = new { challengeId = challenge.ChallengeId, code };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify", body);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        await using (var context = fixture.Context())
        {
            var stored = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
            Assert.Null(stored.ConsumedAt);
            Assert.Equal(0, stored.Attempts);
        }
        var wrong = code == "000000" ? "000001" : "000000";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code = wrong, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify", body)).StatusCode);
    }

    [PostgresFact]
    public async Task Verification_and_reset_share_wrong_code_attempt_limit()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = sender.CodeFor(registration.PhoneChallenge.ChallengeId) })).StatusCode);
        var start = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        var challenge = (await start.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = sender.CodeFor(challenge.ChallengeId);
        var wrong = code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
                new { challengeId = challenge.ChallengeId, code = wrong })).StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
                new { challengeId = challenge.ChallengeId, code = wrong, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
            new { challengeId = challenge.ChallengeId, code })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code, newPassword = NewPassword })).StatusCode);
        await using var context = fixture.Context();
        var stored = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        Assert.Equal(5, stored.Attempts);
        Assert.NotNull(stored.InvalidatedAt);
    }

    [PostgresFact]
    public async Task Sms_reset_changes_password_and_revokes_existing_session()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var login = new { identifier = phone, password = OldPassword, clientType = "mobile" };
        var session = (await (await client.PostAsJsonAsync("/api/v1/auth/login", login))
            .Content.ReadFromJsonAsync<SessionDto>())!;
        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel = "sms", identifier = phone });
        Assert.Equal(HttpStatusCode.Accepted, resetResponse.StatusCode);
        var challenge = (await resetResponse.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var code = sender.CodeFor(challenge.ChallengeId);
        var wrong = code == "000000" ? "000001" : "000000";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code = wrong, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code, newPassword = NewPassword })).StatusCode);
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(me)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { clientType = "mobile", refreshToken = session.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", login)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = NewPassword, clientType = "mobile" })).StatusCode);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Email_reset_works_with_or_without_prior_email_verification(bool verifyEmail)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"reset.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var session = (await (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = OldPassword, clientType = "mobile" }))
            .Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var before = await client.GetAsync("/api/v1/me/verification-status");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal("no-store", before.Headers.CacheControl?.ToString());
        Assert.Equal(new AccountVerificationStatusDto(true, false),
            await before.Content.ReadFromJsonAsync<AccountVerificationStatusDto>());
        var emailSender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        if (verifyEmail)
        {
            var verifyResponse = await client.PostAsync("/api/v1/auth/email-verifications", null);
            Assert.Equal(HttpStatusCode.Accepted, verifyResponse.StatusCode);
            var verify = (await verifyResponse.Content.ReadFromJsonAsync<ChallengeDto>())!;
            var delivery = emailSender.DeliveryFor(verify.ChallengeId);
            Assert.Equal(ChallengePurpose.EmailVerification, delivery.Purpose);
            Assert.Equal(verify.ExpiresAt, delivery.ExpiresAt);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/email-verifications/confirm",
                new { challengeId = verify.ChallengeId, code = emailSender.CodeFor(verify.ChallengeId) })).StatusCode);
        }
        Assert.Equal(new AccountVerificationStatusDto(true, verifyEmail),
            await client.GetFromJsonAsync<AccountVerificationStatusDto>("/api/v1/me/verification-status"));
        client.DefaultRequestHeaders.Authorization = null;
        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel = "email", identifier = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Accepted, resetResponse.StatusCode);
        var reset = (await resetResponse.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var resetDelivery = emailSender.DeliveryFor(reset.ChallengeId);
        Assert.Equal(ChallengePurpose.PasswordReset, resetDelivery.Purpose);
        Assert.Equal(reset.ExpiresAt, resetDelivery.ExpiresAt);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify",
            new { challengeId = reset.ChallengeId, code = emailSender.CodeFor(reset.ChallengeId) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = reset.ChallengeId, code = emailSender.CodeFor(reset.ChallengeId), newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = OldPassword, clientType = "mobile" })).StatusCode);
        var newSessionResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = NewPassword, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, newSessionResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/verification-status")).StatusCode);
        var newSession = (await newSessionResponse.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newSession.AccessToken);
        Assert.Equal(new AccountVerificationStatusDto(true, verifyEmail),
            await client.GetFromJsonAsync<AccountVerificationStatusDto>("/api/v1/me/verification-status"));
    }

    [PostgresFact]
    public async Task Verification_status_requires_authentication()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/verification-status")).StatusCode);
    }

    [PostgresTheory]
    [InlineData(AccountStatus.PendingPhoneVerification)]
    [InlineData(AccountStatus.Blocked)]
    [InlineData(AccountStatus.PendingDeletion)]
    public async Task Reset_request_does_not_reveal_unknown_or_inactive_email(AccountStatus status)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"unverified.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        await using (var context = fixture.Context())
        {
            var account = await context.Accounts.SingleAsync(row => row.Id == registration.UserId);
            account.Status = status;
            await context.SaveChangesAsync();
        }
        var existing = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel = "email", identifier = email });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel = "email", identifier = $"missing.{phone[3..]}@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, existing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        var emailSender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        Assert.False(emailSender.HasCodeFor((await existing.Content.ReadFromJsonAsync<ChallengeDto>())!.ChallengeId));
        Assert.False(emailSender.HasCodeFor((await unknown.Content.ReadFromJsonAsync<ChallengeDto>())!.ChallengeId));
    }

    [PostgresFact]
    public async Task Repeated_reset_request_during_cooldown_does_not_send_another_code()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var sender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var first = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        var firstChallenge = (await first.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var second = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var secondChallenge = (await second.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.NotEqual(firstChallenge.ChallengeId, secondChallenge.ChallengeId);
        Assert.False(sender.HasCodeFor(secondChallenge.ChallengeId));
        Assert.True(sender.HasCodeFor(firstChallenge.ChallengeId));
    }

    [PostgresFact]
    public async Task Five_wrong_email_verification_codes_lock_the_challenge()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"locked.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var session = (await (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = OldPassword, clientType = "mobile" }))
            .Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var start = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var challenge = (await start.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(challenge.ChallengeId);
        client.DefaultRequestHeaders.Authorization = null;
        var wrong = code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/email-verifications/confirm",
                new { challengeId = challenge.ChallengeId, code = wrong })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/email-verifications/confirm",
            new { challengeId = challenge.ChallengeId, code })).StatusCode);
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == registration.UserId);
        var storedChallenge = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        Assert.Null(account.EmailVerifiedAt);
        Assert.Equal(5, storedChallenge.Attempts);
        Assert.NotNull(storedChallenge.InvalidatedAt);
    }

    [PostgresTheory]
    [InlineData("sms")]
    [InlineData("email")]
    public async Task Five_wrong_password_reset_codes_lock_the_challenge(string channel)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"reset-lock.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "senior");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = OldPassword, clientType = "mobile" })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel, identifier = channel == "sms" ? phone : email });
        var challenge = (await response.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = channel == "sms" ? factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId)
            : factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(challenge.ChallengeId);
        var wrong = code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
                new { challengeId = challenge.ChallengeId, code = wrong, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code, newPassword = NewPassword })).StatusCode);
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == registration.UserId);
        var storedChallenge = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        Assert.Equal(5, storedChallenge.Attempts);
        Assert.NotNull(storedChallenge.InvalidatedAt);
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success,
            factory.Services.GetRequiredService<Zpi.Modules.Identity.Infrastructure.Authentication.PasswordHashing>()
                .Verify(account, OldPassword));
    }

    [PostgresFact]
    public async Task Email_verification_and_password_reset_codes_cannot_be_used_interchangeably()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"separate-codes.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = OldPassword, clientType = "mobile" });
        var session = (await login.Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var verification = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.Accepted, verification.StatusCode);
        var emailChallenge = (await verification.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var sender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        var emailCode = sender.CodeFor(emailChallenge.ChallengeId);
        var reset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "email", identifier = email });
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
        var resetChallenge = (await reset.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var resetCode = sender.CodeFor(resetChallenge.ChallengeId);
        client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = emailChallenge.ChallengeId, code = emailCode, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/email-verifications/confirm",
            new { challengeId = resetChallenge.ChallengeId, code = resetCode })).StatusCode);
        await using (var context = fixture.Context())
        {
            var challenges = await context.VerificationChallenges
                .Where(row => row.Id == emailChallenge.ChallengeId || row.Id == resetChallenge.ChallengeId)
                .ToListAsync();
            Assert.Equal(2, challenges.Count);
            Assert.All(challenges, challenge =>
            {
                Assert.Equal(0, challenge.Attempts);
                Assert.Null(challenge.ConsumedAt);
            });
            var account = await context.Accounts.SingleAsync(row => row.Id == registration.UserId);
            Assert.Null(account.EmailVerifiedAt);
            Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success,
                factory.Services.GetRequiredService<Zpi.Modules.Identity.Infrastructure.Authentication.PasswordHashing>().Verify(account, OldPassword));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/email-verifications/confirm",
            new { challengeId = emailChallenge.ChallengeId, code = emailCode })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = resetChallenge.ChallengeId, code = resetCode, newPassword = NewPassword })).StatusCode);
    }

    [PostgresFact]
    public async Task Email_delivery_failure_returns_503_for_verification_and_keeps_reset_accounts_private()
    {
        await using var factory = fixture.Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailCodeSender, FailingEmailCodeSender>()));
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"smtp-failure.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        var session = (await (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = OldPassword, clientType = "mobile" })).Content.ReadFromJsonAsync<SessionDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var verification = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, verification.StatusCode);
        Assert.Contains("deliveryUnavailable", await verification.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var identifier in new[] { email, "missing." + email })
        {
            var reset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "email", identifier });
            Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
            Assert.NotNull(await reset.Content.ReadFromJsonAsync<ChallengeDto>());
        }
    }

    [PostgresFact]
    public async Task Email_verification_resend_invalidates_the_old_code_and_stops_after_confirmation()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"verification.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = OldPassword, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken);
        const string path = "/api/v1/auth/email-verifications";
        using var original = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Accepted, original.StatusCode);
        var challenge = (await original.Content.ReadFromJsonAsync<ChallengeDto>())!;
        using var throttled = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

        await using var context = fixture.Context();
        var saved = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        saved.ResendAfter = DateTimeOffset.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();

        using var resent = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);
        var current = (await resent.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var sender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        using var oldCode = await client.PostAsJsonAsync(path + "/confirm",
            new { challengeId = challenge.ChallengeId, code = sender.CodeFor(challenge.ChallengeId) });
        Assert.Equal(HttpStatusCode.BadRequest, oldCode.StatusCode);
        using var confirmed = await client.PostAsJsonAsync(path + "/confirm",
            new { challengeId = current.ChallengeId, code = sender.CodeFor(current.ChallengeId) });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        using var alreadyVerified = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Conflict, alreadyVerified.StatusCode);
    }

    [PostgresTheory]
    [InlineData("email")]
    [InlineData("sms")]
    public async Task Reset_code_stops_working_after_the_account_contact_changes(string channel)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"contact-reset.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);

        using var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel, identifier = channel == "sms" ? phone : email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var challenge = (await response.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = channel == "sms" ? factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId)
            : factory.Services.GetRequiredService<TestEmailCodeSender>().CodeFor(challenge.ChallengeId);
        await using var context = fixture.Context();
        var account = await context.Accounts.SingleAsync(row => row.Id == registration.UserId);
        if (channel == "sms")
        {
            account.PhoneNumber = Phone();
            account.NormalizedPhoneNumber = account.PhoneNumber;
        }
        else
        {
            account.Email = "updated." + email;
            account.NormalizedEmail = account.Email.ToUpperInvariant();
        }
        await context.SaveChangesAsync();

        using var verify = await client.PostAsJsonAsync("/api/v1/auth/password-resets/verify", new { challengeId = challenge.ChallengeId, code });
        Assert.Equal(HttpStatusCode.BadRequest, verify.StatusCode);
        using var reset = await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = challenge.ChallengeId, code, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        var stored = await context.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        Assert.Null(stored.ConsumedAt);
        Assert.Equal(0, stored.Attempts);
    }

    [PostgresFact]
    public async Task Reissued_reset_code_invalidates_the_previous_code_across_delivery_channels()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"reset-resend.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneSender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        using var confirmed = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneSender.CodeFor(registration.PhoneChallenge.ChallengeId) });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        const string requestPath = "/api/v1/auth/password-reset-requests";
        using var initial = await client.PostAsJsonAsync(requestPath, new { channel = "sms", identifier = phone });
        Assert.Equal(HttpStatusCode.Accepted, initial.StatusCode);
        var challenge = (await initial.Content.ReadFromJsonAsync<ChallengeDto>())!;

        await using var db = fixture.Context();
        var original = await db.VerificationChallenges.SingleAsync(row => row.Id == challenge.ChallengeId);
        original.ResendAfter = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();

        using var repeated = await client.PostAsJsonAsync(requestPath, new { channel = "email", identifier = email });
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        var current = (await repeated.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var emailSender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        const string verifyPath = "/api/v1/auth/password-resets/verify";
        using var oldCode = await client.PostAsJsonAsync(verifyPath,
            new { challengeId = challenge.ChallengeId, code = phoneSender.CodeFor(challenge.ChallengeId) });
        Assert.Equal(HttpStatusCode.BadRequest, oldCode.StatusCode);
        using var newCode = await client.PostAsJsonAsync(verifyPath,
            new { challengeId = current.ChallengeId, code = emailSender.CodeFor(current.ChallengeId) });
        Assert.Equal(HttpStatusCode.NoContent, newCode.StatusCode);
    }

    [PostgresTheory]
    [InlineData("email")]
    [InlineData("sms")]
    public async Task Reset_reports_unavailable_channels_consistently_for_known_and_unknown_accounts(string channel)
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"unavailable-reset.{phone[3..]}@example.test";
        var registration = await Register(client, phone, email, "caretaker");
        var phoneSender = factory.Services.GetRequiredService<TestPhoneCodeSender>();
        var emailSender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        using var confirmed = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneSender.CodeFor(registration.PhoneChallenge.ChallengeId) });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        phoneSender.IsAvailable = channel == "email";
        emailSender.IsAvailable = channel == "sms";
        foreach (var identifier in channel == "sms" ? new[] { phone, Phone() } : [email, "missing." + email])
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel, identifier });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("deliveryUnavailable", await response.Content.ReadAsStringAsync());
        }
        await using var db = fixture.Context();
        Assert.False(await db.VerificationChallenges.AnyAsync(row => row.AccountId == registration.UserId && row.Purpose == ChallengePurpose.PasswordReset));
    }

    [PostgresFact]
    public async Task Email_verification_waits_for_an_address_and_a_working_delivery_channel()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        using var confirmed = await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = OldPassword, clientType = "mobile" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SessionDto>())!.AccessToken);

        using var noAddress = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.Conflict, noAddress.StatusCode);
        await using var db = fixture.Context();
        var account = await db.Accounts.SingleAsync(row => row.Id == registration.UserId);
        account.Email = $"new.{phone[3..]}@example.test";
        account.NormalizedEmail = account.Email.ToUpperInvariant();
        await db.SaveChangesAsync();
        var email = factory.Services.GetRequiredService<TestEmailCodeSender>();
        email.IsAvailable = false;
        using var unavailable = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        email.IsAvailable = true;
        using var accepted = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
    }

    private sealed class FailingEmailCodeSender : IEmailCodeSender
    {
        public bool IsAvailable => true;
        public Task SendAsync(EmailCodeDelivery delivery, CancellationToken cancellationToken) =>
            throw new EmailDeliveryException();
    }

    private static async Task<RegistrationDto> Register(HttpClient client, string phone, string? email, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role, firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email, password = OldPassword });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
    }
}
