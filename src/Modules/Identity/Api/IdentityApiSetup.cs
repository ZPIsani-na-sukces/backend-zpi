using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.Identity.Api;

public static class IdentityApiSetup
{
    public static IServiceCollection AddIdentityApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });
        return services;
    }
}
