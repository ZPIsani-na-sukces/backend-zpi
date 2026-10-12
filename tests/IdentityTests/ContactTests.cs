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
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.SeniorProfile.Application.Contacts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class ContactTests(IdentityApiFixture fixture)
{
    private const string Password = "TestPass1";

    [PostgresFact]
    public async Task Caretaker_can_manage_contacts_and_senior_can_read_them_with_soft_delete_and_audit()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var path = Contacts(senior.Id);
        var created = await caretaker.Client.PostAsJsonAsync(path, Body("  Anna  "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contact = (await created.Content.ReadFromJsonAsync<ContactDto>())!;
        var item = path + "/" + contact.ContactId;
        Assert.Equal(item, created.Headers.Location!.AbsolutePath);
        Assert.Equal("\"1\"", created.Headers.ETag!.Tag);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        Assert.Equal('7', contact.ContactId.ToString()[14]);
        Assert.Equal("Anna", contact.DisplayName);
        Assert.Equal("Jan", contact.FirstName);
        Assert.Equal(caretaker.Id, contact.CreatedBy!.UserId);
        Assert.Equal("caretaker", contact.CreatedBy.Role);
        Assert.Equal(senior.Id, contact.SeniorId);
        Assert.Equal(contact, await senior.Client.GetFromJsonAsync<ContactDto>(item));
        var active = (await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path))!;
        Assert.Equal(contact, Assert.Single(active.Items));
        Assert.Equal(1, active.TotalCount);
        var updated = await Send(caretaker.Client, HttpMethod.Put, item, Body("Zofia", false), "\"1\"");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("\"2\"", updated.Headers.ETag!.Tag);
        var saved = (await updated.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.False(saved.IsActive);
        Assert.Equal(contact.CreatedAt, saved.CreatedAt);
        Assert.Empty((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path))!.Items);
        Assert.Equal(saved, Assert.Single((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?isActive=false"))!.Items));
        Assert.Equal(saved, await senior.Client.GetFromJsonAsync<ContactDto>(item));
        Assert.Equal(HttpStatusCode.NoContent, (await Send(caretaker.Client, HttpMethod.Delete, item, etag: "\"2\"")).StatusCode);
        await Error(await senior.Client.GetAsync(item), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Put, item, Body("Restore"), "\"3\""), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Delete, item, etag: "\"3\""), 404, "resourceNotFound");
        Assert.Empty((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?isActive=false"))!.Items);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SeniorProfileDbContext>();
        var stored = await context.SeniorContactPersons.SingleAsync(row => row.Id == contact.ContactId);
        Assert.Equal(3, stored.Version);
        Assert.Equal(caretaker.Id, stored.CreatedByAccountId);
        Assert.Equal(caretaker.Id, stored.UpdatedByAccountId);
        Assert.Equal(caretaker.Id, stored.DeletedByAccountId);
        Assert.Equal(stored.UpdatedAt, stored.DeletedAt);
        Assert.False(context.Database.HasPendingModelChanges());
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>())
            await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        Assert.False(await context.SeniorContactPersons.AnyAsync(row => row.SeniorId == senior.Id));
    }

    [PostgresFact]
    public async Task Contacts_require_current_version_for_update_and_delete()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var contact = await Create(caretaker, senior.Id, "Anna");
        var path = Contacts(senior.Id) + "/" + contact.ContactId;
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
        {
            var body = method == HttpMethod.Put ? Body("New") : null;
            await Error(await Send(caretaker.Client, method, path, body), 428, "preconditionRequired");
            foreach (var tag in new[] { "1", "\"0\"", "W/\"1\"", "*", "\"1\", \"2\"", "\"2147483648\"" })
                await Error(await Send(caretaker.Client, method, path, body, tag), 400, "validationFailed");
            await Error(await Send(caretaker.Client, method, path, body, "\"2\""), 412, "versionMismatch");
        }
        var response = await caretaker.Client.GetAsync(path);
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
        Assert.Equal(contact, await response.Content.ReadFromJsonAsync<ContactDto>());
    }

    [PostgresFact]
    public async Task Concurrent_contact_updates_allow_only_one_write_for_the_same_version()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var contact = await Create(caretaker, senior.Id, "Anna");
        var path = Contacts(senior.Id) + "/" + contact.ContactId;
        var responses = await Task.WhenAll(
            Send(caretaker.Client, HttpMethod.Put, path, Body("First"), "\"1\""),
            Send(caretaker.Client, HttpMethod.Put, path, Body("Second"), "\"1\""));
        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        await Error(Assert.Single(responses, response => response.StatusCode == HttpStatusCode.PreconditionFailed), 412, "versionMismatch");
        var saved = (await success.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.Equal(2, saved.Version);
        Assert.Equal(saved, await senior.Client.GetFromJsonAsync<ContactDto>(path));
    }

    [PostgresFact]
    public async Task Other_users_cannot_access_contacts_and_seniors_cannot_modify_them()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var otherSenior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var outsider = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Link(otherSenior, caretaker);
        var contact = await Create(caretaker, senior.Id, "Anna");
        var path = Contacts(senior.Id);
        var item = path + "/" + contact.ContactId;
        foreach (var actor in new[] { outsider, otherSenior })
        {
            await Error(await actor.Client.GetAsync(path), 404, "resourceNotFound");
            await Error(await actor.Client.GetAsync(item), 404, "resourceNotFound");
        }
        await Error(await outsider.Client.PostAsJsonAsync(path, Body("Other")), 404, "resourceNotFound");
        await Error(await Send(outsider.Client, HttpMethod.Put, item, Body("Other"), "\"1\""), 404, "resourceNotFound");
        await Error(await Send(outsider.Client, HttpMethod.Delete, item, etag: "\"1\""), 404, "resourceNotFound");
        await Error(await senior.Client.PostAsJsonAsync(path, Body("Own")), 403, "operationForbidden");
        await Error(await Send(senior.Client, HttpMethod.Put, item, Body("Own"), "\"1\""), 403, "operationForbidden");
        await Error(await Send(senior.Client, HttpMethod.Delete, item, etag: "\"1\""), 403, "operationForbidden");
        var wrongParent = Contacts(otherSenior.Id) + "/" + contact.ContactId;
        await Error(await caretaker.Client.GetAsync(wrongParent), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Put, wrongParent, Body("Other"), "\"1\""), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Delete, wrongParent, etag: "\"1\""), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync(Contacts(caretaker.Id), Body("Not senior")), 404, "resourceNotFound");
        Assert.Equal(contact, await senior.Client.GetFromJsonAsync<ContactDto>(item));
    }

    [PostgresFact]
    public async Task Ending_care_or_deactivating_senior_removes_contact_access_immediately()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var contact = await Create(caretaker, senior.Id, "Anna");
        var path = Contacts(senior.Id);
        var item = path + "/" + contact.ContactId;
        Assert.Equal(HttpStatusCode.NoContent, (await caretaker.Client.DeleteAsync($"/api/v1/care-relationships/{relationship.RelationshipId}")).StatusCode);
        await Error(await caretaker.Client.GetAsync(path), 404, "resourceNotFound");
        await Error(await caretaker.Client.GetAsync(item), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync(path, Body("New")), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Put, item, Body("New"), "\"1\""), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Delete, item, etag: "\"1\""), 404, "resourceNotFound");
        Assert.Equal(contact, await senior.Client.GetFromJsonAsync<ContactDto>(item));
        await Link(senior, caretaker);
        await using var identity = fixture.Context();
        var account = await identity.Accounts.SingleAsync(row => row.Id == senior.Id);
        account.Status = AccountStatus.PendingDeletion;
        account.SecurityStamp = Guid.CreateVersion7();
        await identity.SaveChangesAsync();
        await Error(await caretaker.Client.GetAsync(item), 404, "resourceNotFound");
        await Error(await caretaker.Client.PostAsJsonAsync(path, Body("New")), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Put, item, Body("New"), "\"1\""), 404, "resourceNotFound");
        await Error(await Send(caretaker.Client, HttpMethod.Delete, item, etag: "\"1\""), 404, "resourceNotFound");
        await Error(await senior.Client.GetAsync(item), 401, "unauthenticated");
    }

    [PostgresFact]
    public async Task Validation_rejects_invalid_contacts_and_untrusted_audit_fields()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var path = Contacts(senior.Id);
        var valid = Body("Anna");
        var invalid = new[]
        {
            valid with { DisplayName = " " }, valid with { DisplayName = new string('a', 101) },
            valid with { FirstName = new string('a', 101) }, valid with { LastName = new string('a', 101) },
            valid with { RelationshipLabel = new string('a', 101) }, valid with { PhoneNumber = "123" },
            valid with { PhoneNumber = "+012345" }, valid with { PhoneNumber = "+48123456789\n" },
            valid with { PhoneNumber = "+" + new string('1', 16) }
        };
        foreach (var body in invalid) await Error(await caretaker.Client.PostAsJsonAsync(path, body), 400, "validationFailed");
        await Error(await caretaker.Client.PostAsJsonAsync(path, new
        {
            displayName = "Anna", firstName = (string?)null, lastName = (string?)null,
            relationshipLabel = (string?)null, phoneNumber = "+48123456789", isActive = true, createdByAccountId = senior.Id
        }), 400, "validationFailed");
        var contact = await Create(caretaker, senior.Id, "Anna");
        await Error(await Send(caretaker.Client, HttpMethod.Put, path + "/" + contact.ContactId, invalid[0], "\"1\""), 400, "validationFailed");
        Assert.Equal(contact, await senior.Client.GetFromJsonAsync<ContactDto>(path + "/" + contact.ContactId));
        Assert.Equal(1, (await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path))!.TotalCount);
    }

    [PostgresFact]
    public async Task Contact_lists_filter_search_sort_and_paginate_without_returning_other_seniors()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var otherSenior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Link(otherSenior, caretaker);
        var path = Contacts(senior.Id);
        await Create(caretaker, senior.Id, "Zofia");
        var first = await Create(caretaker, senior.Id, "Anna");
        var second = await Create(caretaker, senior.Id, "Anna");
        var last = await Create(caretaker, senior.Id, "Anna%", label: "Rodzina");
        var inactive = await Create(caretaker, senior.Id, "Disabled", false);
        await Create(caretaker, otherSenior.Id, "Other senior");
        var page = (await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?limit=1&page=2"))!;
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.Limit);
        Assert.Equal(second.ContactId, Assert.Single(page.Items).ContactId);
        var all = (await caretaker.Client.GetFromJsonAsync<PageDto<ContactDto>>(path))!;
        Assert.Equal(first.ContactId, all.Items[0].ContactId);
        Assert.All(all.Items, row => Assert.Equal(senior.Id, row.SeniorId));
        Assert.Equal(last.ContactId, Assert.Single((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?search=rodzina"))!.Items).ContactId);
        Assert.Equal(last.ContactId, Assert.Single((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?search=%25"))!.Items).ContactId);
        var newest = (await caretaker.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?sort=-createdAt"))!;
        Assert.Equal(last.ContactId, newest.Items[0].ContactId);
        Assert.Equal(inactive.ContactId, Assert.Single((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?isActive=false"))!.Items).ContactId);
        Assert.Empty((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(path + "?page=100"))!.Items);
        foreach (var query in new[] { "page=0", "page=2147483647", "limit=0", "limit=101", "sort=phoneNumber", "search=" + new string('a', 101), "isActive=abc" })
            await Error(await caretaker.Client.GetAsync(path + "?" + query), 400, "validationFailed");
    }

    [PostgresFact]
    public async Task Contacts_remain_readable_when_the_creator_is_deactivated_or_permanently_deleted()
    {
        await using var factory = fixture.Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var contact = await Create(caretaker, senior.Id, "Anna");
        var path = Contacts(senior.Id) + "/" + contact.ContactId;
        await using var identity = fixture.Context();
        var account = await identity.Accounts.SingleAsync(row => row.Id == caretaker.Id);
        account.Status = AccountStatus.PendingDeletion;
        await identity.SaveChangesAsync();
        Assert.Equal(caretaker.Id, (await senior.Client.GetFromJsonAsync<ContactDto>(path))!.CreatedBy!.UserId);
        identity.Accounts.Remove(account);
        await identity.SaveChangesAsync();
        Assert.Null((await senior.Client.GetFromJsonAsync<ContactDto>(path))!.CreatedBy);
        Assert.Null(Assert.Single((await senior.Client.GetFromJsonAsync<PageDto<ContactDto>>(Contacts(senior.Id)))!.Items).CreatedBy);
    }

    [PostgresFact]
    public async Task Anonymous_users_cannot_access_any_contact_endpoint()
    {
        await using var factory = fixture.Factory();
        using var client = factory.CreateClient();
        var path = Contacts(Guid.CreateVersion7());
        var item = path + "/" + Guid.CreateVersion7();
        await Error(await client.GetAsync(path), 401, "unauthenticated");
        await Error(await client.GetAsync(item), 401, "unauthenticated");
        await Error(await client.PostAsJsonAsync(path, Body("Anna")), 401, "unauthenticated");
        await Error(await Send(client, HttpMethod.Put, item, Body("Anna"), "\"1\""), 401, "unauthenticated");
        await Error(await Send(client, HttpMethod.Delete, item, etag: "\"1\""), 401, "unauthenticated");
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
        var created = await caretaker.Client.PostAsJsonAsync("/api/v1/care-invitations", new { recipientIdentifier = senior.Phone });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invitation = (await created.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsync($"/api/v1/care-invitations/{invitation.InvitationId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }

    private static string Contacts(Guid seniorId) => $"/api/v1/seniors/{seniorId}/contacts";
    private static SaveContactRequest Body(string name, bool active = true, string? label = "córka") =>
        new(name, " Jan ", null, label, "+48123456789", active);

    private static async Task<ContactDto> Create(TestActor caretaker, Guid seniorId, string name, bool active = true, string? label = "córka")
    {
        var response = await caretaker.Client.PostAsJsonAsync(Contacts(seniorId), Body(name, active, label));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContactDto>())!;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? etag = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
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
