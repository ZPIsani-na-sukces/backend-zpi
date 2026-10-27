using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Zpi.Database;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Modules.Notifications.Infrastructure.Persistence;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.IdentityTests;

[CollectionDefinition("Identity API")]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_DATABASE")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_REDIS")))
            Skip = "Set ZPI_TEST_DATABASE and ZPI_TEST_REDIS to run integration tests.";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_DATABASE")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_REDIS")))
            Skip = "Set ZPI_TEST_DATABASE and ZPI_TEST_REDIS to run integration tests.";
    }
}

public sealed class IdentityApiFixture : IAsyncLifetime
{
    private string? adminConnection;
    private string? databaseName;
    public string RedisConnectionString { get; private set; } = "";
    public string MedicalStorageRoot { get; } = Path.Combine(Path.GetTempPath(), "zpi-medical-tests-" + Guid.CreateVersion7().ToString("N"));
    public string RedisPrefix { get; } = $"zpi:test:{Guid.CreateVersion7():N}:";
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var original = Environment.GetEnvironmentVariable("ZPI_TEST_DATABASE");
        RedisConnectionString = Environment.GetEnvironmentVariable("ZPI_TEST_REDIS") ?? "";
        if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(RedisConnectionString)) return;
        var builder = new NpgsqlConnectionStringBuilder(original);
        adminConnection = builder.ConnectionString;
        databaseName = "zpi_identity_tests_" + Guid.CreateVersion7().ToString("N");
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
        await create.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        ConnectionString = builder.ConnectionString;
        await using var context = Context();
        await context.Database.MigrateAsync();
        await using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var type in new[] { typeof(CareDbContext), typeof(SeniorProfileDbContext), typeof(SafetyDbContext),
            typeof(RoutinesDbContext), typeof(MedicalRecordsDbContext), typeof(NotificationsDbContext) })
            await ((DbContext)scope.ServiceProvider.GetRequiredService(type)).Database.MigrateAsync();
    }

    public IdentityDbContext Context()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseConfiguration.Configure(options, ConnectionString, IdentityDbContext.Schema);
        return new IdentityDbContext(options.Options);
    }

    public WebApplicationFactory<Program> Factory() => new ApiFactory(ConnectionString, RedisConnectionString, RedisPrefix, MedicalStorageRoot);

    public async Task DisposeAsync()
    {
        if (databaseName is null || adminConnection is null) return;
        using var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
        foreach (var endpoint in redis.GetEndPoints())
        {
            await foreach (var key in redis.GetServer(endpoint).KeysAsync(database: redis.GetDatabase().Database, pattern: RedisPrefix + "*"))
                await redis.GetDatabase().KeyDeleteAsync(key);
        }
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
        if (Directory.Exists(MedicalStorageRoot)) Directory.Delete(MedicalStorageRoot, true);
    }

    private sealed class ApiFactory(string connectionString, string redisConnectionString, string redisPrefix, string medicalStorageRoot) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["ConnectionStrings:Redis"] = redisConnectionString,
                ["Redis:KeyPrefix"] = redisPrefix,
                ["MedicalRecords:StorageProvider"] = "Local",
                ["MedicalRecords:StorageRoot"] = medicalStorageRoot,
                ["MedicalRecords:CleanupWorkerEnabled"] = "false",
                ["AccountDeletion:Enabled"] = "false",
                ["Care:CleanupWorkerEnabled"] = "false",
                ["Routines:WorkerEnabled"] = "false",
                ["Notifications:WorkerEnabled"] = "false",
                ["Authentication:SigningKey"] = new string('k', 64)
            }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TestPhoneCodeSender>();
                services.AddSingleton<IPhoneCodeSender>(provider => provider.GetRequiredService<TestPhoneCodeSender>());
                services.AddSingleton<TestEmailCodeSender>();
                services.AddSingleton<IEmailCodeSender>(provider => provider.GetRequiredService<TestEmailCodeSender>());
            });
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["ConnectionStrings:Redis"] = redisConnectionString,
                ["Redis:KeyPrefix"] = redisPrefix,
                ["MedicalRecords:StorageProvider"] = "Local",
                ["MedicalRecords:StorageRoot"] = medicalStorageRoot,
                ["MedicalRecords:CleanupWorkerEnabled"] = "false",
                ["AccountDeletion:Enabled"] = "false",
                ["Care:CleanupWorkerEnabled"] = "false",
                ["Routines:WorkerEnabled"] = "false",
                ["Notifications:WorkerEnabled"] = "false",
                ["Authentication:SigningKey"] = new string('k', 64)
            }));
        }
    }
}

public sealed class TestPhoneCodeSender : IPhoneCodeSender
{
    private readonly ConcurrentDictionary<Guid, string> codes = new();

    public bool IsAvailable => true;

    public Task SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken)
    {
        codes[challengeId] = code;
        return Task.CompletedTask;
    }

    public string CodeFor(Guid challengeId) => codes[challengeId];
    public bool HasCodeFor(Guid challengeId) => codes.ContainsKey(challengeId);
}

public sealed class TestEmailCodeSender : IEmailCodeSender
{
    private readonly ConcurrentDictionary<Guid, string> codes = new();

    public bool IsAvailable => true;

    public Task SendAsync(Guid challengeId, string email, string code, CancellationToken cancellationToken)
    {
        codes[challengeId] = code;
        return Task.CompletedTask;
    }

    public string CodeFor(Guid challengeId) => codes[challengeId];
    public bool HasCodeFor(Guid challengeId) => codes.ContainsKey(challengeId);
}
