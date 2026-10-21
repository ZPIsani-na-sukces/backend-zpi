using Microsoft.Extensions.Configuration;
using Zpi.Api;

namespace Zpi.IdentityTests;

public sealed class LocalEnvironmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Env_is_loaded_from_the_repository_with_correct_configuration_priority(bool overrideSecret)
    {
        var root = Path.Combine(Path.GetTempPath(), "zpi-env-" + Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(root, "src", "Api");
        Directory.CreateDirectory(contentRoot);
        var isolatedKey = "ZPI_ENV_TEST_" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(Path.Combine(root, "Zpi.slnx"), "");
            File.WriteAllText(Path.Combine(root, ".env"),
                "Safety__Geocoding__ApiKey='pk.test+&?=' # private\n" + isolatedKey + "=local\n");
            File.WriteAllText(Path.Combine(root, "appsettings.json"), """{"Safety":{"Geocoding":{"ApiKey":""}}}""");
            var configuration = new ConfigurationManager();
            configuration.AddJsonFile(Path.Combine(root, "appsettings.json"));
            if (overrideSecret) configuration.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Safety:Geocoding:ApiKey"] = "higher-priority" });
            configuration.AddLocalEnvironment(contentRoot);
            Assert.Equal(overrideSecret ? "higher-priority" : "pk.test+&?=", configuration["Safety:Geocoding:ApiKey"]);
            Assert.Equal("local", configuration[isolatedKey]);
            Assert.Null(Environment.GetEnvironmentVariable(isolatedKey));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Malformed_env_does_not_expose_secret_values_in_the_exception()
    {
        var root = Path.Combine(Path.GetTempPath(), "zpi-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, ".env"), "Safety__Geocoding__ApiKey='private-secret");
            var configuration = new ConfigurationManager();
            var error = Assert.Throws<InvalidOperationException>(() => configuration.AddLocalEnvironment(root));
            Assert.DoesNotContain("private-secret", error.ToString());
        }
        finally { Directory.Delete(root, true); }
    }
}
