using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Api;

internal sealed class DevelopmentDatabaseMigrationService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        DbContext[] contexts =
        [
            services.GetRequiredService<IdentityDbContext>(),
            services.GetRequiredService<CareDbContext>(),
            services.GetRequiredService<SeniorProfileDbContext>(),
            services.GetRequiredService<SafetyDbContext>(),
            services.GetRequiredService<RoutinesDbContext>(),
            services.GetRequiredService<MedicalRecordsDbContext>(),
            services.GetRequiredService<NotificationsDbContext>()
        ];

        foreach (var context in contexts)
            await context.Database.MigrateAsync(cancellationToken);

        await SeedDevelopmentDataAsync(services, cancellationToken);
    }

    private static async Task SeedDevelopmentDataAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var identityContext = services.GetRequiredService<IdentityDbContext>();
        var careContext = services.GetRequiredService<CareDbContext>();
        var safetyContext = services.GetRequiredService<SafetyDbContext>();
        var passwords = services.GetRequiredService<PasswordHashing>();
        var clock = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var now = clock.GetUtcNow();

        var seniorId = Guid.Parse("019a0000-0000-7000-8000-000000000010");
        var caretakerId = Guid.Parse("019a0000-0000-7000-8000-000000000020");
        var invitationId = Guid.Parse("019a0000-0000-7000-8000-000000000030");
        var relationshipId = Guid.Parse("019a0000-0000-7000-8000-000000000040");
        var deviceId = Guid.Parse("019a0000-0000-7000-8000-000000000050");
        var deviceInstallationId = Guid.Parse("019a0000-0000-7000-8000-000000000051");
        var locationSampleId = Guid.Parse("019a0000-0000-7000-8000-000000000060");
        var batterySampleId = Guid.Parse("019a0000-0000-7000-8000-000000000061");
        var homeAddressId = Guid.Parse("019a0000-0000-7000-8000-000000000070");

        var seniorRole = await identityContext.Roles.SingleOrDefaultAsync(r => r.Code == AccountRole.Senior, cancellationToken);
        var caretakerRole = await identityContext.Roles.SingleOrDefaultAsync(r => r.Code == AccountRole.Caretaker, cancellationToken);

        if (seniorRole is null || caretakerRole is null) return;

        var senior = await identityContext.Accounts.FirstOrDefaultAsync(
            a => a.Id == seniorId || a.NormalizedPhoneNumber == "+48111222333" || a.NormalizedEmail == "SENIOR@EXAMPLE.COM", cancellationToken);
        if (senior is null)
        {
            senior = new Account
            {
                Id = seniorId,
                RoleId = seniorRole.Id,
                Email = "senior@example.com",
                NormalizedEmail = "SENIOR@EXAMPLE.COM",
                PhoneNumber = "+48111222333",
                NormalizedPhoneNumber = "+48111222333",
                FirstName = "Jan",
                LastName = "Kowalski",
                Status = AccountStatus.Active,
                PhoneVerifiedAt = now,
                EmailVerifiedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                PasswordHash = ""
            };
            senior.PasswordHash = passwords.Hash(senior, "Password123!");
            identityContext.Accounts.Add(senior);
            await identityContext.SaveChangesAsync(cancellationToken);
        }

        var caretaker = await identityContext.Accounts.FirstOrDefaultAsync(
            a => a.Id == caretakerId || a.NormalizedPhoneNumber == "+48999888777" || a.NormalizedEmail == "OPIEKUN@EXAMPLE.COM", cancellationToken);
        if (caretaker is null)
        {
            caretaker = new Account
            {
                Id = caretakerId,
                RoleId = caretakerRole.Id,
                Email = "opiekun@example.com",
                NormalizedEmail = "OPIEKUN@EXAMPLE.COM",
                PhoneNumber = "+48999888777",
                NormalizedPhoneNumber = "+48999888777",
                FirstName = "Anna",
                LastName = "Nowak",
                Status = AccountStatus.Active,
                PhoneVerifiedAt = now,
                EmailVerifiedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                PasswordHash = ""
            };
            caretaker.PasswordHash = passwords.Hash(caretaker, "Password123!");
            identityContext.Accounts.Add(caretaker);
            await identityContext.SaveChangesAsync(cancellationToken);
        }

        var invitation = await careContext.SeniorCareInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
        if (invitation is null)
        {
            invitation = new SeniorCareInvitation
            {
                Id = invitationId,
                SeniorId = senior.Id,
                CaretakerId = caretaker.Id,
                SenderAccountId = caretaker.Id,
                RecipientAccountId = senior.Id,
                Status = InvitationStatus.Accepted,
                CreatedAt = now,
                ExpiresAt = now.AddDays(7),
                RespondedAt = now
            };
            careContext.SeniorCareInvitations.Add(invitation);
            await careContext.SaveChangesAsync(cancellationToken);
        }

        var relationshipExists = await careContext.SeniorCareRelationships.AnyAsync(
            r => r.Id == relationshipId || (r.SeniorId == senior.Id && r.CaretakerId == caretaker.Id && r.Status == RelationshipStatus.Active), cancellationToken);
        if (!relationshipExists)
        {
            var relationship = new SeniorCareRelationship
            {
                Id = relationshipId,
                InvitationId = invitation.Id,
                SeniorId = senior.Id,
                CaretakerId = caretaker.Id,
                Status = RelationshipStatus.Active,
                CreatedAt = now
            };
            careContext.SeniorCareRelationships.Add(relationship);
            await careContext.SaveChangesAsync(cancellationToken);
        }

        var device = await safetyContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId || d.AssignedAccountId == senior.Id, cancellationToken);
        if (device is null)
        {
            device = new Device
            {
                Id = deviceId,
                AssignedAccountId = senior.Id,
                InstallationId = deviceInstallationId,
                Platform = DevicePlatform.Android,
                AppVersion = "1.0.0",
                MonitoringEnabled = true,
                RegisteredAt = now,
                LastOnlineAt = now
            };
            safetyContext.Devices.Add(device);
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        var locationSampleExists = await safetyContext.LocationSamples.AnyAsync(l => l.Id == locationSampleId || l.DeviceId == device.Id, cancellationToken);
        if (!locationSampleExists)
        {
            var locationSample = new LocationSample
            {
                Id = locationSampleId,
                DeviceId = device.Id,
                Latitude = 51.107883,
                Longitude = 17.038538,
                AccuracyMeters = 5.0,
                SpeedMps = 0.0,
                CapturedAt = now,
                ReceivedAt = now
            };
            safetyContext.LocationSamples.Add(locationSample);
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        var batterySampleExists = await safetyContext.BatterySamples.AnyAsync(b => b.Id == batterySampleId || b.DeviceId == device.Id, cancellationToken);
        if (!batterySampleExists)
        {
            var batterySample = new BatterySample
            {
                Id = batterySampleId,
                DeviceId = device.Id,
                BatteryLevelPercent = 85,
                IsCharging = false,
                CapturedAt = now,
                ReceivedAt = now
            };
            safetyContext.BatterySamples.Add(batterySample);
            await safetyContext.SaveChangesAsync(cancellationToken);
        }

        var homeAddressExists = await safetyContext.HomeAddresses.AnyAsync(h => h.Id == homeAddressId || (h.SeniorId == senior.Id && h.DeletedAt == null), cancellationToken);
        if (!homeAddressExists)
        {
            var homeAddress = new HomeAddress
            {
                Id = homeAddressId,
                SeniorId = senior.Id,
                CountryCode = "PL",
                PostalCode = "50-001",
                City = "Wrocław",
                Street = "Rynek",
                BuildingNumber = "1",
                ApartmentNumber = "10",
                Latitude = 51.107883,
                Longitude = 17.038538,
                CreatedByAccountId = senior.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            safetyContext.HomeAddresses.Add(homeAddress);
            await safetyContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
