using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Zpi.IdentityTests;

[Collection("Identity API")]
public sealed class ApiDocumentationTests(IdentityApiFixture fixture)
{
    [PostgresTheory]
    [InlineData("/docs")]
    [InlineData("/openapi/v1.json")]
    public async Task Api_documentation_is_served_without_a_session_in_production(string path)
    {
        await using var factory = fixture.Factory().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
