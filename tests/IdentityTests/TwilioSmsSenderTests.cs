using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Infrastructure.Sms;

namespace Zpi.IdentityTests;

public sealed class TwilioSmsSenderTests
{
    private const string AccountSid = "AC0123456789abcdef0123456789abcdef";
    private const string AuthToken = "test-auth-token";
    private const string From = "+15005550006";

    [Theory]
    [InlineData("Production", false, typeof(UnavailableSmsSender))]
    [InlineData("Development", false, typeof(DevelopmentSmsSender))]
    [InlineData("Production", true, typeof(TwilioSmsSender))]
    [InlineData("Development", true, typeof(TwilioSmsSender))]
    public void Twilio_is_used_whenever_it_is_configured(string environment, bool configured, Type expected)
    {
        using var services = Services(configured ? Configured() : new Dictionary<string, string?>(), environment);
        Assert.IsType(expected, services.GetRequiredService<ISmsSender>());
        var codes = Assert.IsType<PhoneCodeSender>(services.GetRequiredService<IPhoneCodeSender>());
        Assert.Equal(expected != typeof(UnavailableSmsSender), codes.IsAvailable);
    }

    [Theory]
    [InlineData("Development", SmsSendStatus.Sent)]
    [InlineData("Production", SmsSendStatus.Unavailable)]
    public async Task Unconfigured_sms_returns_the_correct_environment_specific_delivery_status(string environment, SmsSendStatus status)
    {
        var logs = new RecordingLogs();
        using var services = Services(new Dictionary<string, string?>(), environment, logs: logs);
        Assert.Equal(new SmsSendResult(status), await SendSms(services));
        Assert.Equal(status == SmsSendStatus.Sent, logs.Messages.Any(message => message.Contains("Development SMS")));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void The_allowlist_wraps_twilio_in_every_environment(string environment)
    {
        using var services = Services(Allowlisted(), environment);
        Assert.IsType<AllowlistedSmsSender>(services.GetRequiredService<ISmsSender>());
        Assert.True(services.GetRequiredService<IPhoneCodeSender>().IsAvailable);
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, "[]", SmsSendStatus.Sent)]
    [InlineData(HttpStatusCode.Created, "{\"sid\":17}", SmsSendStatus.Sent)]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":\"21211\"}", SmsSendStatus.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":2147483648}", SmsSendStatus.Unavailable)]
    public async Task Unexpected_provider_fields_do_not_create_delivery_ids_or_classify_numbers_as_invalid(
        HttpStatusCode status, string body, SmsSendStatus expected)
    {
        using var services = Services(Configured(), handler: new FakeTwilio(status, body));
        Assert.Equal(new SmsSendResult(expected), await SendSms(services));
    }

    [Theory]
    [InlineData("48123456789")]
    [InlineData("+48123456789,abc")]
    public void Invalid_allowlist_numbers_are_rejected(string allowedNumbers)
    {
        var settings = Configured();
        settings["Twilio:AllowedNumbers"] = allowedNumbers;
        using var services = Services(settings, "Staging");
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<TwilioOptions>>().Value);
    }

    [Fact]
    public async Task Only_allowlisted_numbers_get_a_real_sms_and_others_get_the_fixed_code()
    {
        var twilio = new FakeTwilio(HttpStatusCode.Created, "{}");
        using var services = Services(Allowlisted(), "Staging", twilio);
        var codes = services.GetRequiredService<IPhoneVerificationCodes>();

        Assert.Null(codes.FixedCodeFor("+48123456789"));
        Assert.Equal("123456", codes.FixedCodeFor("+48999888777"));

        Assert.Equal(PhoneCodeDelivery.Sent, await Send(services, phoneNumber: "+48999888777"));
        Assert.Null(twilio.Uri);
        Assert.Equal(PhoneCodeDelivery.Sent, await Send(services, phoneNumber: "+48111222333"));
        Assert.Equal("+48111222333", QueryHelpers.ParseQuery(twilio.Body)["To"].ToString());
    }

    [Fact]
    public async Task Messages_to_numbers_outside_the_allowlist_are_only_logged()
    {
        var twilio = new FakeTwilio(HttpStatusCode.Created, """{"sid":"SM0123"}""");
        var logs = new RecordingLogs();
        using var services = Services(Allowlisted(), "Staging", twilio, logs);

        Assert.Equal(new SmsSendResult(SmsSendStatus.Sent), await SendSms(services, phoneNumber: "+48999888777"));
        Assert.Null(twilio.Uri);
        Assert.Contains(logs.Messages, message => message.Contains("notification delivery 42") && message.Contains("Twilio:AllowedNumbers"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Without_an_active_allowlist_no_number_gets_the_fixed_code(bool twilioConfigured)
    {
        var settings = twilioConfigured ? Configured() : new Dictionary<string, string?>();
        if (!twilioConfigured) settings["Twilio:AllowedNumbers"] = "+48123456789";
        using var services = Services(settings, "Development");
        Assert.Null(services.GetRequiredService<IPhoneVerificationCodes>().FixedCodeFor("+48999888777"));
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
        Assert.Equal("Kod weryfikacyjny: 042137", form["Body"].ToString());
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

    [Fact]
    public async Task Sends_the_given_message_unchanged_and_returns_the_twilio_sid()
    {
        const string message = "Jan Kowalski jest poza bezpiecznym obszarem „Dom”. Otwórz aplikację, aby sprawdzić zgłoszenie.";
        var twilio = new FakeTwilio(HttpStatusCode.Created, """{"sid":"SM0123","status":"queued"}""");
        using var services = Services(Configured(), handler: twilio);

        Assert.Equal(new SmsSendResult(SmsSendStatus.Sent, "SM0123"), await SendSms(services, message: message));
        var form = QueryHelpers.ParseQuery(twilio.Body);
        Assert.Equal("+48123456789", form["To"].ToString());
        Assert.Equal(From, form["From"].ToString());
        Assert.Equal(message, form["Body"].ToString());
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

    private static Dictionary<string, string?> Allowlisted()
    {
        var settings = Configured();
        settings["Twilio:AllowedNumbers"] = "+48123456789, +48111222333";
        return settings;
    }

    private static ServiceProvider Services(Dictionary<string, string?> settings, string environment = "Production",
        HttpMessageHandler? handler = null, RecordingLogs? logs = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            if (logs is not null) logging.AddProvider(logs);
        });
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environment });
        services.AddIdentitySms(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        if (handler is not null)
            services.AddHttpClient(TwilioSmsSender.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static Task<PhoneCodeDelivery> Send(ServiceProvider services, CancellationToken cancellationToken = default,
        string phoneNumber = "+48123456789") =>
        services.GetRequiredService<IPhoneCodeSender>().SendAsync(Guid.CreateVersion7(), phoneNumber, "042137", cancellationToken);

    private static Task<SmsSendResult> SendSms(ServiceProvider services, string phoneNumber = "+48123456789",
        string message = "Wiadomość testowa") =>
        services.GetRequiredService<ISmsSender>().SendAsync("notification delivery 42", phoneNumber, message, CancellationToken.None);

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

    private sealed class RecordingLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Messages);

        public void Dispose() { }

        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
