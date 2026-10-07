using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class PasswordResetTests(IdentityApiFixture fixture)
{
    private const string OldPassword = "A long password 123!";
    private const string NewPassword = "A newer password 456!";
    private static string Phone() => "+48" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);

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

    [PostgresFact]
    public async Task Email_must_be_verified_before_it_can_reset_a_password()
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
        var premature = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel = "email", identifier = email });
        Assert.Equal(HttpStatusCode.Accepted, premature.StatusCode);
        var fake = (await premature.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = fake.ChallengeId, code = "anything", newPassword = NewPassword })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var verifyResponse = await client.PostAsync("/api/v1/auth/email-verifications", null);
        Assert.Equal(HttpStatusCode.Accepted, verifyResponse.StatusCode);
        var verify = (await verifyResponse.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var emailSender = factory.Services.GetRequiredService<TestEmailCodeSender>();
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/email-verifications/confirm",
            new { challengeId = verify.ChallengeId, code = emailSender.CodeFor(verify.ChallengeId) })).StatusCode);
        var resetResponse = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests",
            new { channel = "email", identifier = email });
        Assert.Equal(HttpStatusCode.Accepted, resetResponse.StatusCode);
        var reset = (await resetResponse.Content.ReadFromJsonAsync<ChallengeDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password-resets",
            new { challengeId = reset.ChallengeId, code = emailSender.CodeFor(reset.ChallengeId), newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = email, password = NewPassword, clientType = "mobile" })).StatusCode);
    }

    [PostgresFact]
    public async Task Reset_request_does_not_reveal_unknown_or_unverified_email()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var email = $"unverified.{phone[3..]}@example.test";
        await Register(client, phone, email, "caretaker");
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

    [PostgresFact]
    public async Task Five_wrong_password_reset_codes_lock_the_challenge()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var phone = Phone();
        var registration = await Register(client, phone, null, "senior");
        var phoneCode = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(registration.PhoneChallenge.ChallengeId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/phone-verifications/confirm",
            new { challengeId = registration.PhoneChallenge.ChallengeId, code = phoneCode })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = phone, password = OldPassword, clientType = "mobile" })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { channel = "sms", identifier = phone });
        var challenge = (await response.Content.ReadFromJsonAsync<ChallengeDto>())!;
        var code = factory.Services.GetRequiredService<TestPhoneCodeSender>().CodeFor(challenge.ChallengeId);
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

    private static async Task<RegistrationDto> Register(HttpClient client, string phone, string? email, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/registrations",
            new { role, firstName = "Jan", lastName = "Kowalski", phoneNumber = phone, email, password = OldPassword });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
    }
}
