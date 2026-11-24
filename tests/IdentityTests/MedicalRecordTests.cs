using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Application.Records;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Modules.MedicalRecords.Infrastructure.Storage;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed partial class MedicalRecordTests(IdentityApiFixture fixture)
{
    private const string Password = "MedicalPassword123!";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF\n");
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j3ioAAAAASUVORK5CYII=");

    [PostgresFact]
    public async Task Caretaker_creates_private_files_and_both_roles_can_download_with_no_storage_keys_in_json()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "wynik.pdf"), (Png, "image/png", "zdjęcie.png"));
        Assert.Equal(2, record.Files.Count);
        Assert.Equal(caretaker.Id, record.CreatedBy!.UserId);
        var response = await senior.Client.GetAsync(PathFor(senior.Id, record.RecordId));
        await Status(response, 200);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("objectKey", json);
        Assert.DoesNotContain("checksum", json);
        Assert.DoesNotContain("filePath", json);
        var downloaded = await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, Assert.Single(record.Files, file => file.MimeType == "application/pdf").FileId));
        await Status(downloaded, 200);
        Assert.Equal(Pdf, await downloaded.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", downloaded.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Pdf.Length, downloaded.Content.Headers.ContentLength);
        Assert.True(downloaded.Headers.CacheControl!.NoStore && downloaded.Headers.CacheControl.Private);
        Assert.Equal("nosniff", Assert.Single(downloaded.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("inline", downloaded.Content.Headers.ContentDisposition!.DispositionType);
        var stored = await StoredFiles(factory, record.RecordId);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Pdf)), stored.Single(row => row.MimeType == "application/pdf").ChecksumSha256);
        foreach (var file in stored) Assert.True(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, file.ObjectKey)));
    }

    [PostgresFact]
    public async Task Metadata_blob_and_plain_json_form_fields_are_supported_without_requiring_attachments()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        foreach (var blob in new[] { false, true })
        {
            using var form = Form(Metadata("  Recepta  "), blob);
            var response = await caretaker.Client.PostAsync(Base(senior.Id), form);
            await Status(response, 201);
            var record = (await response.Content.ReadFromJsonAsync<MedicalRecordDto>())!;
            Assert.Equal("Recepta", record.Title);
            Assert.Empty(record.Files);
        }
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Full_length_unicode_metadata_is_accepted_in_forms_and_updates(bool blob)
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var metadata = Metadata(new string('ą', 200)) with { Description = new string('ę', 4000) };
        using var form = Form(metadata, blob);
        using var response = await caretaker.Client.PostAsync(Base(senior.Id), form);
        await Status(response, 201);
        var record = (await response.Content.ReadFromJsonAsync<MedicalRecordDto>())!;
        Assert.Equal(metadata.Description, record.Description);
        var updated = metadata with { Title = new string('ó', 200), Description = new string('ż', 4000) };
        using var changed = await Change(caretaker, HttpMethod.Put, PathFor(senior.Id, record.RecordId), 1, updated);
        await Status(changed, 200);
        Assert.Equal(updated.Description, (await changed.Content.ReadFromJsonAsync<MedicalRecordDto>())!.Description);
    }

    [PostgresFact]
    public async Task Range_and_attachment_responses_use_authorized_streams_and_safe_headers()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "wynik.pdf"));
        var path = ContentPath(senior.Id, record.RecordId, record.Files[0].FileId);
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Range = new RangeHeaderValue(0, 4);
        var partial = await senior.Client.SendAsync(request);
        await Status(partial, 206);
        Assert.Equal(Pdf[..5], await partial.Content.ReadAsByteArrayAsync());
        Assert.Equal("bytes 0-4/" + Pdf.Length, partial.Content.Headers.ContentRange!.ToString());
        var invalid = new HttpRequestMessage(HttpMethod.Get, path);
        invalid.Headers.Range = new RangeHeaderValue(Pdf.Length + 10, null);
        await Status(await senior.Client.SendAsync(invalid), 416);
        var attachment = await caretaker.Client.GetAsync(path + "?disposition=attachment");
        await Status(attachment, 200);
        Assert.Equal("attachment", attachment.Content.Headers.ContentDisposition!.DispositionType);
        await Status(await senior.Client.GetAsync(path + "?disposition=invalid"), 400);
    }

    [PostgresFact]
    public async Task Writes_require_caretaker_role_and_all_reads_require_the_current_care_relationship()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        using var stranger = await Actor(factory, AccountRole.Caretaker);
        var relationship = await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "wynik.pdf"));
        var recordPath = PathFor(senior.Id, record.RecordId);
        var filePath = recordPath + "/files/" + record.Files[0].FileId;
        using var form = Form(Metadata());
        await Status(await senior.Client.PostAsync(Base(senior.Id), form), 403);
        await Status(await stranger.Client.GetAsync(Base(senior.Id)), 404);
        await Status(await stranger.Client.GetAsync(ContentPath(senior.Id, record.RecordId, record.Files[0].FileId)), 404);
        await Status(await Change(senior, HttpMethod.Delete, PathFor(senior.Id, record.RecordId), 1), 403);
        await Status(await caretaker.Client.DeleteAsync("/api/v1/care-relationships/" + relationship.RelationshipId), 204);
        await Status(await caretaker.Client.GetAsync(PathFor(senior.Id, record.RecordId)), 404);
        await Status(await caretaker.Client.GetAsync(ContentPath(senior.Id, record.RecordId, record.Files[0].FileId)), 404);
        await Status(await Change(caretaker, HttpMethod.Put, recordPath, 1, Metadata()), 404);
        await Status(await Change(caretaker, HttpMethod.Delete, recordPath, 1), 404);
        await Status(await Change(caretaker, HttpMethod.Delete, filePath, 1), 404);
        await Status(await Add(caretaker, senior.Id, record.RecordId, 1), 404);
        using var denied = Form(Metadata());
        await Status(await caretaker.Client.PostAsync(Base(senior.Id), denied), 404);
        await Status(await senior.Client.GetAsync(recordPath), 200);
    }

    [PostgresFact]
    public async Task File_cannot_be_requested_through_a_different_record_or_senior()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var first = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "first.pdf"));
        var second = await Create(caretaker, senior.Id);
        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, second.RecordId, first.Files[0].FileId)), 404);
        await Status(await senior.Client.GetAsync(ContentPath(Guid.CreateVersion7(), first.RecordId, first.Files[0].FileId)), 404);
        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, Guid.CreateVersion7(), first.Files[0].FileId)), 404);
        await Status(await Add(caretaker, senior.Id, Guid.CreateVersion7(), 1), 404);
        using var anonymous = factory.CreateClient();
        await Status(await anonymous.GetAsync(ContentPath(senior.Id, first.RecordId, first.Files[0].FileId)), 401);
    }

    [PostgresFact]
    public async Task Metadata_updates_and_file_changes_share_the_parent_etag_and_the_last_file_can_be_removed()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id);
        var path = PathFor(senior.Id, record.RecordId);
        await Status(await caretaker.Client.PutAsJsonAsync(path, Metadata(), JsonOptions), 428);
        var updated = await Change(caretaker, HttpMethod.Put, path, 1, Metadata("Nowy tytuł"));
        await Status(updated, 200);
        Assert.Equal("\"2\"", updated.Headers.ETag!.Tag);
        await Status(await Add(caretaker, senior.Id, record.RecordId, 1), 412);
        var added = await Add(caretaker, senior.Id, record.RecordId, 2);
        await Status(added, 201);
        Assert.Equal("\"3\"", added.Headers.ETag!.Tag);
        var file = (await added.Content.ReadFromJsonAsync<MedicalFileDto>())!;
        await Status(await Change(caretaker, HttpMethod.Put, path, 2, Metadata()), 412);
        await Status(await Change(caretaker, HttpMethod.Delete, path, 2), 412);
        await Status(await Change(caretaker, HttpMethod.Delete, path + "/files/" + file.FileId, 2), 412);
        var deleted = await Change(caretaker, HttpMethod.Delete, path + "/files/" + file.FileId, 3);
        await Status(deleted, 204);
        Assert.Equal("\"4\"", deleted.Headers.ETag!.Tag);
        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, file.FileId)), 404);
        Assert.Empty((await senior.Client.GetFromJsonAsync<MedicalRecordDto>(path))!.Files);
        await Cleanup(factory);
        var stored = Assert.Single(await StoredFiles(factory, record.RecordId));
        Assert.NotNull(stored.PurgedAt);
        Assert.False(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, stored.ObjectKey)));
    }

    [PostgresFact]
    public async Task Missing_storage_object_returns_not_found_without_recording_a_download()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "missing.pdf"));
        var stored = Assert.Single(await StoredFiles(factory, record.RecordId));
        File.Delete(System.IO.Path.Combine(fixture.MedicalStorageRoot, stored.ObjectKey));

        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, stored.Id)), 404);
        await Status(await senior.Client.GetAsync(PathFor(senior.Id, record.RecordId)), 200);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        Assert.False(await database.MedicalAuditEntries.AnyAsync(row => row.SeniorId == senior.Id && row.Action == "download"));
    }

    [PostgresFact]
    public async Task Invalid_file_signatures_mime_empty_content_and_metadata_leave_no_partial_record()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var before = Directory.Exists(fixture.MedicalStorageRoot) ? Directory.GetFiles(fixture.MedicalStorageRoot).Length : 0;
        using (var form = Form(Metadata(), false, (Pdf, "application/pdf", "ok.pdf"), (Png, "application/pdf", "wrong.pdf")))
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 400);
        Assert.Equal(before, Directory.Exists(fixture.MedicalStorageRoot) ? Directory.GetFiles(fixture.MedicalStorageRoot).Length : 0);
        foreach (var file in new[] { (Array.Empty<byte>(), "application/pdf", "empty.pdf"), (Pdf, "text/html", "fake.html") })
        {
            using var form = Form(Metadata(), false, file);
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 400);
        }
        using (var form = Form(Metadata() with { DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) }))
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 400);
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent("{broken-json"), "metadata");
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 400);
        }
        Assert.Empty((await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id)))!.Items);
    }

    [PostgresFact]
    public async Task File_limits_apply_to_both_create_and_add_and_can_be_lowered_in_configuration()
    {
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["MedicalRecords:MaxFilesPerRecord"] = "1", ["MedicalRecords:MaxFileSizeBytes"] = "128" })));
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        using (var form = Form(Metadata(), false, (Pdf, "application/pdf", "a.pdf"), (Pdf, "application/pdf", "b.pdf")))
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 400);
        using (var form = Form(Metadata(), false, (new byte[129], "application/pdf", "big.pdf")))
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 400);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "a.pdf"));
        await Status(await Add(caretaker, senior.Id, record.RecordId, 1), 409);
    }

    [PostgresFact]
    public async Task Pagination_filters_and_stable_sorting_are_applied_in_the_database()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        foreach (var title in new[] { "Beta", "Alfa", "Gamma" })
        {
            using var form = Form(Metadata(title) with { DocumentDate = date });
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 201);
        }
        var initial = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id) + "?sort=title&limit=1"))!;
        Assert.NotNull(initial.NextCursor);
        var page = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id) + "?sort=title&cursor=" + initial.NextCursor + "&limit=1"))!;
        Assert.Equal("Beta", Assert.Single(page.Items).Title);
        foreach (var sort in new[] { "title", "documentDate", "-documentDate", "-createdAt" })
        {
            var expected = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id) + "?sort=" + sort))!;
            var ids = new List<Guid>();
            string? cursor = null;
            do
            {
                var portion = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(
                    Base(senior.Id) + "?sort=" + sort + "&limit=1" + (cursor is null ? "" : "&cursor=" + cursor)))!;
                ids.AddRange(portion.Items.Select(row => row.RecordId));
                cursor = portion.NextCursor;
                Assert.InRange(ids.Count, 1, 3);
            } while (cursor is not null);
            Assert.Equal(expected.Items.Select(row => row.RecordId), ids);
        }
        var filtered = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id) + "?search=alF&recordType=testResult"))!;
        Assert.Equal("Alfa", Assert.Single(filtered.Items).Title);
        var dateOnly = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var sameDay = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(
            Base(senior.Id) + $"?documentDateFrom={dateOnly}&documentDateTo={dateOnly}"))!;
        Assert.Equal(3, sameDay.Items.Count);
        await Status(await senior.Client.GetAsync(Base(senior.Id) + "?sort=unknown"), 400);
        await Status(await senior.Client.GetAsync(Base(senior.Id) + "?documentDateFrom=2026-10-16&documentDateTo=2026-10-15"), 400);
    }

    [PostgresFact]
    public async Task Search_treats_percent_underscore_and_backslash_as_literal_text()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        foreach (var title in new[] { "Wynik 100%", "A_B", @"C\D", "Other" })
        {
            using var form = Form(Metadata(title));
            await Status(await caretaker.Client.PostAsync(Base(senior.Id), form), 201);
        }
        foreach (var search in new[] { "%", "_", @"\" })
        {
            var page = (await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id) + "?search=" + Uri.EscapeDataString(search)))!;
            Assert.Contains(search, Assert.Single(page.Items).Title);
        }
    }

    [PostgresFact]
    public async Task Deleting_a_record_hides_files_immediately_and_cleanup_physically_removes_them()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "file.pdf"));
        await Status(await Change(caretaker, HttpMethod.Delete, PathFor(senior.Id, record.RecordId), 1), 204);
        await Status(await senior.Client.GetAsync(PathFor(senior.Id, record.RecordId)), 404);
        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, record.Files[0].FileId)), 404);
        await Cleanup(factory);
        var file = Assert.Single(await StoredFiles(factory, record.RecordId));
        Assert.NotNull(file.DeletedAt);
        Assert.NotNull(file.PurgedAt);
        Assert.False(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, file.ObjectKey)));
    }

    [PostgresFact]
    public async Task Concurrent_file_additions_with_the_same_etag_cannot_overwrite_each_other()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id);
        var responses = await Task.WhenAll(Add(caretaker, senior.Id, record.RecordId, 1), Add(caretaker, senior.Id, record.RecordId, 1));
        Assert.Single(responses, row => row.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, row => row.StatusCode == HttpStatusCode.PreconditionFailed);
        Assert.Single((await senior.Client.GetFromJsonAsync<MedicalRecordDto>(PathFor(senior.Id, record.RecordId)))!.Files);
    }

    [PostgresFact]
    public async Task Audit_contains_actions_and_ids_without_medical_titles_and_the_schema_matches_migrations()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "private-name.pdf"));
        await Status(await senior.Client.GetAsync(PathFor(senior.Id, record.RecordId)), 200);
        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, record.Files[0].FileId)), 200);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        var audit = await db.MedicalAuditEntries.Where(row => row.SeniorId == senior.Id).ToArrayAsync();
        Assert.Contains(audit, row => row.Action == "create" && row.ActorAccountId == caretaker.Id);
        Assert.Contains(audit, row => row.Action == "download" && row.ActorAccountId == senior.Id);
        Assert.DoesNotContain("private-name", JsonSerializer.Serialize(audit));
        Assert.DoesNotContain(record.Title, JsonSerializer.Serialize(audit));
    }

    [PostgresFact]
    public async Task Permanent_account_cleanup_removes_physical_files_and_all_senior_metadata()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var otherSenior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        await Link(otherSenior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "file.pdf"));
        var otherRecord = await Create(caretaker, otherSenior.Id, (Pdf, "application/pdf", "other.pdf"));
        var stored = Assert.Single(await StoredFiles(factory, record.RecordId));
        var otherFile = Assert.Single(await StoredFiles(factory, otherRecord.RecordId));
        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>())
        {
            await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
            await cleanup.DeleteAsync(senior.Id, CancellationToken.None);
        }
        Assert.False(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, stored.ObjectKey)));
        var db = scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        Assert.False(await db.MedicalRecords.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.False(await db.MedicalFiles.AnyAsync(row => row.MedicalRecordId == record.RecordId));
        Assert.False(await db.MedicalAuditEntries.AnyAsync(row => row.SeniorId == senior.Id));
        Assert.True(await db.MedicalRecords.AnyAsync(row => row.Id == otherRecord.RecordId));
        Assert.True(await db.MedicalFiles.AnyAsync(row => row.Id == otherFile.Id));
        Assert.True(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, otherFile.ObjectKey)));
    }

    [PostgresFact]
    public async Task Orphan_cleanup_removes_old_unreferenced_objects_and_staging_but_preserves_active_files()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "file.pdf"));
        var stored = Assert.Single(await StoredFiles(factory, record.RecordId));
        var orphan = System.IO.Path.Combine(fixture.MedicalStorageRoot, Guid.CreateVersion7().ToString("N"));
        var staging = System.IO.Path.Combine(fixture.MedicalStorageRoot, Guid.CreateVersion7().ToString("N") + ".tmp");
        var referenced = System.IO.Path.Combine(fixture.MedicalStorageRoot, stored.ObjectKey);
        await File.WriteAllBytesAsync(orphan, Pdf);
        await File.WriteAllBytesAsync(staging, Pdf);
        foreach (var path in new[] { orphan, staging, referenced }) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        await Cleanup(factory);
        Assert.False(File.Exists(orphan));
        Assert.False(File.Exists(staging));
        Assert.True(File.Exists(referenced));
    }

    [PostgresFact]
    public async Task Orphan_cleanup_processes_a_full_batch_and_resumes_on_the_next_run()
    {
        var root = System.IO.Path.Combine(fixture.MedicalStorageRoot, Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(root);
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["MedicalRecords:StorageRoot"] = root })));
        for (var index = 0; index < 101; index++)
        {
            var path = System.IO.Path.Combine(root, Guid.CreateVersion7().ToString("N"));
            await File.WriteAllBytesAsync(path, Pdf);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        }

        await Cleanup(factory);
        Assert.Single(Directory.EnumerateFiles(root));
        await Cleanup(factory);
        Assert.Empty(Directory.EnumerateFiles(root));
    }
    [PostgresFact]
    public async Task Storage_failure_returns_safe_503_and_does_not_create_a_partial_record()
    {
        await using var factory = Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IMedicalFileStorage>(provider => new UnreliableStorage(
                ActivatorUtilities.CreateInstance<LocalMedicalFileStorage>(provider), failSave: true))));
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        using var form = Form(Metadata(), false, (Pdf, "application/pdf", "secret.pdf"));
        var response = await caretaker.Client.PostAsync(Base(senior.Id), form);
        await Status(response, 503);
        Assert.Equal("5", Assert.Single(response.Headers.GetValues("Retry-After")));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-storage-detail", raw);
        using var json = JsonDocument.Parse(raw);
        Assert.Equal("temporarilyUnavailable", json.RootElement.GetProperty("code").GetString());
        Assert.Empty((await senior.Client.GetFromJsonAsync<CursorPageDto<MedicalRecordDto>>(Base(senior.Id)))!.Items);
    }

    [PostgresFact]
    public async Task Failed_physical_deletion_keeps_retryable_metadata_and_later_cleanup_succeeds()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "file.pdf"));
        await Status(await Change(caretaker, HttpMethod.Delete, PathFor(senior.Id, record.RecordId), 1), 204);
        await using (var broken = Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IMedicalFileStorage>(provider => new UnreliableStorage(
                ActivatorUtilities.CreateInstance<LocalMedicalFileStorage>(provider), failSave: false)))))
            await Cleanup(broken);
        var pending = Assert.Single(await StoredFiles(factory, record.RecordId));
        Assert.Null(pending.PurgedAt);
        Assert.True(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, pending.ObjectKey)));
        await Cleanup(factory);
        var purged = Assert.Single(await StoredFiles(factory, record.RecordId));
        Assert.NotNull(purged.PurgedAt);
        Assert.False(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, purged.ObjectKey)));
    }

    [PostgresFact]
    public async Task Account_deletion_retains_retryable_metadata_after_partial_storage_failure_and_finishes_on_retry()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "file.pdf"), (Png, "image/png", "scan.png"));
        var files = await StoredFiles(factory, record.RecordId);
        await using (var context = fixture.Context())
        {
            var account = (await context.Accounts.FindAsync(senior.Id))!;
            account.Status = AccountStatus.PendingDeletion;
            account.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-31);
            await context.SaveChangesAsync();
        }
        await using (var broken = Factory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IMedicalFileStorage>(provider => new UnreliableStorage(
                ActivatorUtilities.CreateInstance<LocalMedicalFileStorage>(provider), failSave: false, successfulDeletesBeforeFailure: 1)))))
        {
            await using var scope = broken.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>().ProcessAsync(CancellationToken.None);
        }
        await using (var context = fixture.Context())
            Assert.True(await context.Accounts.AnyAsync(row => row.Id == senior.Id && row.Status == AccountStatus.PendingDeletion));
        Assert.Equal(2, (await StoredFiles(factory, record.RecordId)).Length);
        Assert.Single(files, file => File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, file.ObjectKey)));
        await using var retry = factory.Services.CreateAsyncScope();
        var processor = retry.ServiceProvider.GetRequiredService<AccountDeletionProcessor>();
        await processor.ProcessAsync(CancellationToken.None);
        await processor.ProcessAsync(CancellationToken.None);
        await using (var context = fixture.Context())
        {
            Assert.False(await context.Accounts.AnyAsync(row => row.Id == senior.Id));
            Assert.True(await context.Accounts.AnyAsync(row => row.Id == caretaker.Id));
        }
        var medical = retry.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>();
        Assert.False(await medical.MedicalRecords.AnyAsync(row => row.Id == record.RecordId));
        Assert.False(await medical.MedicalFiles.AnyAsync(row => row.MedicalRecordId == record.RecordId));
        Assert.All(files, file => Assert.False(File.Exists(System.IO.Path.Combine(fixture.MedicalStorageRoot, file.ObjectKey))));
    }

    [PostgresFact]
    public async Task Removed_author_does_not_remove_the_seniors_document_or_files()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "file.pdf"));
        await using (var scope = factory.Services.CreateAsyncScope())
            foreach (var cleanup in scope.ServiceProvider.GetServices<IAccountDataCleanup>()) await cleanup.DeleteAsync(caretaker.Id, CancellationToken.None);
        await using (var context = fixture.Context())
        {
            var row = (await context.Accounts.FindAsync(caretaker.Id))!;
            context.Accounts.Remove(row);
            await context.SaveChangesAsync();
        }
        var after = (await senior.Client.GetFromJsonAsync<MedicalRecordDto>(PathFor(senior.Id, record.RecordId)))!;
        Assert.Null(after.CreatedBy);
        await Status(await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, record.Files[0].FileId)), 200);
    }

    [PostgresFact]
    public async Task Jpeg_signature_is_supported_and_cannot_be_downloaded_as_another_mime_type()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        byte[] jpeg = [255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0, 255, 217];
        var record = await Create(caretaker, senior.Id, (jpeg, "image/jpeg", "scan.jpg"));
        var response = await senior.Client.GetAsync(ContentPath(senior.Id, record.RecordId, record.Files[0].FileId));
        await Status(response, 200);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(jpeg, await response.Content.ReadAsByteArrayAsync());
    }

    [PostgresFact]
    public async Task Generated_openapi_exposes_medical_uploads_downloads_and_shared_errors()
    {
        await using var factory = Factory().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        await Status(response, 200);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        var unavailable = paths.GetProperty("/api/v1/auth/login").GetProperty("post").GetProperty("responses").GetProperty("503");
        Assert.True(unavailable.GetProperty("content").TryGetProperty("application/problem+json", out _));
        var prefix = "/api/v1/seniors/{seniorId}/medical-records";
        var form = paths.GetProperty(prefix).GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data");
        Assert.True(form.GetProperty("schema").GetProperty("properties").TryGetProperty("metadata", out _));
        Assert.Equal("application/json", form.GetProperty("encoding").GetProperty("metadata").GetProperty("contentType").GetString());
        Assert.Equal("binary", form.GetProperty("schema").GetProperty("properties").GetProperty("files").GetProperty("items").GetProperty("format").GetString());
        var download = paths.GetProperty(prefix + "/{recordId}/files/{fileId}/content").GetProperty("get").GetProperty("responses").GetProperty("200");
        Assert.Equal("binary", download.GetProperty("content").GetProperty("application/pdf").GetProperty("schema").GetProperty("format").GetString());
    }

    [PostgresFact]
    public async Task Client_file_names_cannot_choose_the_storage_path()
    {
        await using var factory = Factory();
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "../../outside.pdf"));
        Assert.Equal("outside.pdf", Assert.Single(record.Files).OriginalName);
        var stored = Assert.Single(await StoredFiles(factory, record.RecordId));
        Assert.True(Guid.TryParseExact(stored.ObjectKey, "N", out _));
        var path = System.IO.Path.Combine(fixture.MedicalStorageRoot, stored.ObjectKey);
        Assert.True(File.Exists(path));
        if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    private sealed class UnreliableStorage(IMedicalFileStorage inner, bool failSave, int successfulDeletesBeforeFailure = 0) : IMedicalFileStorage
    {
        private int deleteAttempts;
        public Task<StoredMedicalFile> SaveAsync(MedicalUploadFile upload, CancellationToken cancellationToken) => failSave
            ? throw new MedicalStorageException(new IOException("secret-storage-detail")) : inner.SaveAsync(upload, cancellationToken);
        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => inner.OpenReadAsync(key, cancellationToken);
        public Task DeleteAsync(string key, CancellationToken cancellationToken) => deleteAttempts++ < successfulDeletesBeforeFailure
            ? inner.DeleteAsync(key, cancellationToken) : throw new MedicalStorageException(new IOException("secret-storage-detail"));
        public IAsyncEnumerable<string> FindOrphansAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) => inner.FindOrphansAsync(olderThan, cancellationToken);
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
        var created = await caretaker.Client.PostAsync("/api/v1/care-invitations", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invitation = (await created.Content.ReadFromJsonAsync<CareInvitationDto>())!;
        var accepted = await senior.Client.PostAsJsonAsync("/api/v1/care-invitations/accept", new { code = invitation.Code });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (await accepted.Content.ReadFromJsonAsync<CareRelationshipDto>())!;
    }

    private WebApplicationFactory<Program> Factory() => fixture.Factory();
    private static string Base(Guid seniorId) => $"/api/v1/seniors/{seniorId}/medical-records";
    private static string PathFor(Guid seniorId, Guid recordId) => Base(seniorId) + "/" + recordId;
    private static string ContentPath(Guid seniorId, Guid recordId, Guid fileId) => PathFor(seniorId, recordId) + "/files/" + fileId + "/content";
    private static SaveMedicalRecordMetadataRequest Metadata(string title = "Wyniki badań") => new(title, null, MedicalRecordType.TestResult, DateOnly.FromDateTime(DateTime.UtcNow));
    private static MultipartFormDataContent Form(SaveMedicalRecordMetadataRequest metadata, bool blob = false, params (byte[] Bytes, string Mime, string Name)[] files)
    {
        var form = new MultipartFormDataContent();
        var json = new StringContent(JsonSerializer.Serialize(metadata, JsonOptions), Encoding.UTF8, "application/json");
        if (blob) form.Add(json, "metadata", "blob"); else form.Add(json, "metadata");
        foreach (var file in files) form.Add(Part(file.Bytes, file.Mime), "files", file.Name);
        return form;
    }
    private static ByteArrayContent Part(byte[] bytes, string mime)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        return content;
    }
    private static async Task<MedicalRecordDto> Create(TestActor actor, Guid seniorId, params (byte[] Bytes, string Mime, string Name)[] files)
    {
        using var form = Form(Metadata(), false, files);
        var response = await actor.Client.PostAsync(Base(seniorId), form);
        await Status(response, 201);
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);
        return (await response.Content.ReadFromJsonAsync<MedicalRecordDto>())!;
    }
    private static async Task<HttpResponseMessage> Add(TestActor actor, Guid seniorId, Guid recordId, int version, byte[]? bytes = null, string mime = "application/pdf")
    {
        var form = new MultipartFormDataContent();
        form.Add(Part(bytes ?? Pdf, mime), "file", "added.pdf");
        var request = new HttpRequestMessage(HttpMethod.Post, PathFor(seniorId, recordId) + "/files") { Content = form };
        request.Headers.TryAddWithoutValidation("If-Match", "\"" + version + "\"");
        return await actor.Client.SendAsync(request);
    }
    private static Task<HttpResponseMessage> Change(TestActor actor, HttpMethod method, string path, int version, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("If-Match", "\"" + version + "\"");
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return actor.Client.SendAsync(request);
    }
    private static async Task Status(HttpResponseMessage response, int status) => Assert.True((int)response.StatusCode == status,
        $"Expected {status}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private async Task<MedicalFile[]> StoredFiles(WebApplicationFactory<Program> factory, Guid recordId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>().MedicalFiles.AsNoTracking().Where(row => row.MedicalRecordId == recordId).ToArrayAsync();
    }
    private static async Task Cleanup(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MedicalFileCleanupProcessor>().ProcessAsync(CancellationToken.None);
    }
    private sealed record TestActor(Guid Id, string Phone, HttpClient Client) : IDisposable { public void Dispose() => Client.Dispose(); }
}
