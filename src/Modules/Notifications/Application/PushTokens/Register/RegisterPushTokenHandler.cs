using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.PushTokens.Register;

public sealed class RegisterPushTokenHandler(NotificationsDbContext context, IIdentityFacade identity, TimeProvider clock)
{
    public async Task<NotificationResult<PushTokenDto>> Handle(RegisterPushTokenCommand command, CancellationToken cancellationToken)
    {
        if (!await identity.IsSessionActiveAsync(command.AccountId, command.SessionId, cancellationToken))
            return NotificationResult<PushTokenDto>.Failed(NotificationFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = command.Request;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var token = await context.PushDeviceTokens
            .SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);
        if (token is not null && token.AccountId != command.AccountId)
            return NotificationResult<PushTokenDto>.Failed(NotificationFailure.TokenAlreadyRegistered);
        var now = clock.GetUtcNow();
        var previous = await context.PushDeviceTokens
            .Where(row => row.AccountId == command.AccountId &&
            row.InstallationId == request.InstallationId && row.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var row in previous
            .Where(row => row != token)) row.RevokedAt = now;
        var replacedIds = previous
            .Select(row => row.Id)
            .Append(token?.Id ?? Guid.Empty)
            .ToArray();
        var retainedCount = await context.PushDeviceTokens
            .CountAsync(row => row.AccountId == command.AccountId &&
            row.RevokedAt == null && !replacedIds.Contains(row.Id), cancellationToken);
        if (retainedCount >= 10)
            return NotificationResult<PushTokenDto>.Failed(NotificationFailure.TokenLimitReached);
        await context.SaveChangesAsync(cancellationToken);
        if (token is null)
        {
            token = new PushDeviceToken
            {
                AccountId = command.AccountId,
                SessionId = command.SessionId,
                InstallationId = request.InstallationId,
                Platform = request.Platform,
                Token = request.Token,
                TokenHash = hash,
                RegisteredAt = now
            };
            context.PushDeviceTokens.Add(token);
        }
        token.SessionId = command.SessionId;
        token.InstallationId = request.InstallationId;
        token.Platform = request.Platform;
        token.UpdatedAt = now;
        token.RevokedAt = null;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (!await identity.IsSessionActiveAsync(command.AccountId, command.SessionId, cancellationToken))
        {
            await context.PushDeviceTokens
                .Where(row => row.Id == token.Id && row.SessionId == command.SessionId)
                .ExecuteDeleteAsync(cancellationToken);
            return NotificationResult<PushTokenDto>.Failed(NotificationFailure.ResourceNotFound);
        }
        return NotificationResult<PushTokenDto>.Success(new PushTokenDto(token.Id, token.InstallationId, token.Platform.ToCamelCase(), token.RegisteredAt, token.UpdatedAt));
    }
}
