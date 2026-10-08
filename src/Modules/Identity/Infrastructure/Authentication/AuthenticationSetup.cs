using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Infrastructure.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthenticationOptions>().Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<PasswordHashing>();
        services.AddSingleton<TokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthenticationOptions>>((jwt, authentication) =>
            {
                var settings = authentication.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                jwt.IncludeErrorDetails = false;
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var accountId) ||
                            !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessionId))
                        { context.Fail("Invalid session."); return; }
                        var database = context.HttpContext.RequestServices.GetRequiredService<IdentityDbContext>();
                        var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
                        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
                        var session = await sessions.GetAsync(sessionId, context.HttpContext.RequestAborted);
                        var valid = session is not null && session.AccountId == accountId && session.ExpiresAt > now &&
                            await database.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId &&
                                account.Status == AccountStatus.Active && account.PhoneVerifiedAt != null && account.SecurityStamp == session.SecurityStamp, context.HttpContext.RequestAborted);
                        if (!valid) context.Fail("Session is no longer active.");
                    }
                };
            });
        services.AddAuthorization();
        return services;
    }
}
