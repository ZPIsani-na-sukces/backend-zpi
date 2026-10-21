using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;

namespace Zpi.Api;

public static class LocalEnvironmentConfiguration
{
    public static void AddLocalEnvironment(this ConfigurationManager configuration, string contentRoot)
    {
        var directory = new DirectoryInfo(contentRoot);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Zpi.slnx")))
            directory = directory.Parent;
        var path = Path.Combine(directory?.FullName ?? contentRoot, ".env");
        if (!File.Exists(path)) return;
        KeyValuePair<string, string?>[] values;
        try
        {
            values = DotNetEnv.Env.NoEnvVars().Load(path)
                .Select(pair => new KeyValuePair<string, string?>(pair.Key.Replace("__", ":"), pair.Value))
                .ToArray();
        }
        catch (Exception exception) when (exception is not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Invalid .env file. Check the syntax using .env.example.");
        }
        var index = 0;
        for (var position = 0; position < configuration.Sources.Count; position++)
            if (configuration.Sources[position] is JsonConfigurationSource json &&
                Path.GetFileName(json.Path)?.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) == true)
                index = position + 1;
        configuration.Sources.Insert(index, new MemoryConfigurationSource { InitialData = values });
    }
}
