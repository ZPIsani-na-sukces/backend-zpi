using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Infrastructure.Sms;

namespace Zpi.IdentityTests;

public sealed class TwilioPhoneCodeSenderTests
{
    private const string AccountSid = "AC0123456789abcdef0123456789abcdef";
    private const string AuthToken = "test-auth-token";
    private const string From = "+15005550006";

    [Theory]
    [InlineData("Production", false, typeof(UnavailablePhoneCodeSender))]
    [InlineData("Development", false, typeof(DevelopmentPhoneCodeSender))]
    [InlineData("Production", true, typeof(TwilioPhoneCodeSender))]
    [InlineData("Development", true, typeof(TwilioPhoneCodeSender))]
    public void Twilio_is_used_whenever_it_is_configured(string environment, bool configured, Type expected)
    {
        using var services = Services(configured ? Configured() : new Dictionary<string, string?>(), environment);
        Assert.IsType(expected, services.GetRequiredService<IPhoneCodeSender>());
    }

    [Fact]
    public async Task Sends_the_code_from_the_configured_number_with_basic_auth()
    {
        var twilio = new FakeTwilio(HttpStatusCode.Created, """{"sid":"SM0123","status":"queued"}""");
        using var services = Services(Configured(), handler: twilio);

        Assert.Equal(PhoneCodeDelivery.Sent, await Send(services));
        Assert.Equal(HttpMethod.Post, twilio.Method);
        Assert.Equal($"https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json", twilio.Uri);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AccountSid}:{AuthToken}")), twilio.Authorization);
        var form = QueryHelpers.ParseQuery(twilio.Body);
        Assert.Equal("+48123456789", form["To"].ToString());
        Assert.Equal(From, form["From"].ToString());
        Assert.Equal("Kod weryfikacyjny: 042137. Nie podawaj go nikomu.", form["Body"].ToString());
    }

    [Fact]
    public async Task Uses_the_configured_message_template()
    {
        var settings = Configured();
        settings["Twilio:MessageTemplate"] = "ZPI: {code}";
        var twilio = new FakeTwilio(HttpStatusCode.Created, "{}");
        using var services = Services(settings, handler: twilio);

        Assert.Equal(PhoneCodeDelivery.Sent, await Send(services));
        Assert.Equal("ZPI: 042137", QueryHelpers.ParseQuery(twilio.Body)["Body"].ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"code":21211,"message":"Invalid 'To' Phone Number","status":400}""", PhoneCodeDelivery.InvalidPhoneNumber)]
    [InlineData(HttpStatusCode.BadRequest, """{"code":21408}""", PhoneCodeDelivery.InvalidPhoneNumber)]
    [InlineData(HttpStatusCode.BadRequest, """{"code":21614}""", PhoneCodeDelivery.InvalidPhoneNumber)]
    [InlineData(HttpStatusCode.BadRequest, """{"code":21608}""", PhoneCodeDelivery.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, """{"code":"21211"}""", PhoneCodeDelivery.Unavailable)]
    [InlineData(HttpStatusCode.Unauthorized, """{"code":20003}""", PhoneCodeDelivery.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"code":20429}""", PhoneCodeDelivery.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, "not json", PhoneCodeDelivery.Unavailable)]
    public async Task Twilio_errors_map_to_delivery_results(HttpStatusCode status, string response, PhoneCodeDelivery expected)
    {
        using var services = Services(Configured(), handler: new FakeTwilio(status, response));
        Assert.Equal(expected, await Send(services));
    }

    [Fact]
    public async Task Timeouts_and_network_errors_are_reported_as_unavailable()
    {
        foreach (var failure in new Exception[] { new TaskCanceledException("Timed out."), new HttpRequestException("Connection refused.") })
        {
            using var services = Services(Configured(), handler: new FakeTwilio(failure: failure));
            Assert.Equal(PhoneCodeDelivery.Unavailable, await Send(services));
        }
    }

    [Fact]
    public async Task A_cancelled_request_is_not_reported_as_a_delivery_failure()
    {
        using var services = Services(Configured(), handler: new FakeTwilio(HttpStatusCode.Created, "{}"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Send(services, cancelled.Token));
    }

    [Theory]
    [InlineData("Twilio:AccountSid", "AC123")]
    [InlineData("Twilio:AuthToken", "")]
    [InlineData("Twilio:AuthToken", "token with spaces")]
    [InlineData("Twilio:From", "")]
    [InlineData("Twilio:From", "48123456789")]
    [InlineData("Twilio:MessageTemplate", "Twój kod weryfikacyjny")]
    public void Invalid_settings_are_rejected(string key, string value)
    {
        var settings = Configured();
        settings[key] = value;
        using var services = Services(settings);
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<TwilioOptions>>().Value);
    }

    private static Dictionary<string, string?> Configured() => new()
    {
        ["Twilio:AccountSid"] = AccountSid,
        ["Twilio:AuthToken"] = AuthToken,
        ["Twilio:From"] = From
    };

    private static ServiceProvider Services(Dictionary<string, string?> settings, string environment = "Production",
        HttpMessageHandler? handler = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environment });
        services.AddIdentitySms(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        if (handler is not null)
            services.AddHttpClient(TwilioPhoneCodeSender.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static Task<PhoneCodeDelivery> Send(ServiceProvider services, CancellationToken cancellationToken = default) =>
        services.GetRequiredService<IPhoneCodeSender>().SendAsync(Guid.CreateVersion7(), "+48123456789", "042137", cancellationToken);

    private sealed class FakeTwilio(HttpStatusCode status = HttpStatusCode.Created, string response = "{}",
        Exception? failure = null) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Uri { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Method = request.Method;
            Uri = request.RequestUri!.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (failure is not null) throw failure;
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
