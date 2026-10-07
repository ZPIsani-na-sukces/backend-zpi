using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Infrastructure.Redis;

public sealed class RedisSessionStore(IConnectionMultiplexer connection, IOptions<RedisOptions> options, TimeProvider clock) : ISessionStore
{
    public async Task SaveAsync(AccountSession session, CancellationToken cancellationToken)
    {
        var transaction = connection.GetDatabase().CreateTransaction();
        transaction.AddCondition(Condition.KeyNotExists(SessionKey(session.Id)));
        Write(transaction, session);
        if (!await transaction.ExecuteAsync().WaitAsync(cancellationToken))
            throw new InvalidOperationException("Session already exists.");
    }

    public async Task<AccountSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var value = await connection.GetDatabase().StringGetAsync(SessionKey(sessionId)).WaitAsync(cancellationToken);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<AccountSession>(value.ToString());
    }

    public async Task<AccountSession?> FindByRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var value = await connection.GetDatabase().StringGetAsync(RefreshKey(tokenHash)).WaitAsync(cancellationToken);
        return Guid.TryParse(value.ToString(), out var id) ? await GetAsync(id, cancellationToken) : null;
    }

    public async Task<bool> RotateAsync(AccountSession session, string newTokenHash, CancellationToken cancellationToken)
    {
        var transaction = connection.GetDatabase().CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(SessionKey(session.Id), JsonSerializer.Serialize(session)));
        session.RefreshTokenHash = newTokenHash;
        session.ExpiresAt = clock.GetUtcNow().AddDays(30);
        Write(transaction, session);
        return await transaction.ExecuteAsync().WaitAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await GetAsync(sessionId, cancellationToken);
        if (session is null) return;
        var transaction = connection.GetDatabase().CreateTransaction();
        _ = transaction.KeyDeleteAsync(SessionKey(sessionId));
        var hashes = await connection.GetDatabase().SetMembersAsync(TokenHashesKey(sessionId)).WaitAsync(cancellationToken);
        foreach (var hash in hashes)
            _ = transaction.KeyDeleteAsync(RefreshKey(hash.ToString()));
        _ = transaction.KeyDeleteAsync(TokenHashesKey(sessionId));
        _ = transaction.SetRemoveAsync(AccountKey(session.AccountId), sessionId.ToString("N"));
        await transaction.ExecuteAsync().WaitAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var database = connection.GetDatabase();
        var ids = await database.SetMembersAsync(AccountKey(accountId)).WaitAsync(cancellationToken);
        foreach (var id in ids)
        {
            await RevokeAsync(Guid.Parse(id.ToString()), cancellationToken);
            await database.SetRemoveAsync(AccountKey(accountId), id).WaitAsync(cancellationToken);
        }
    }

    private void Write(ITransaction transaction, AccountSession session)
    {
        var ttl = session.ExpiresAt - clock.GetUtcNow();
        if (ttl <= TimeSpan.Zero) throw new ArgumentException("Session must expire in the future.", nameof(session));
        _ = transaction.StringSetAsync(SessionKey(session.Id), JsonSerializer.Serialize(session), ttl);
        _ = transaction.StringSetAsync(RefreshKey(session.RefreshTokenHash), session.Id.ToString("N"), ttl);
        _ = transaction.SetAddAsync(TokenHashesKey(session.Id), session.RefreshTokenHash);
        _ = transaction.KeyExpireAsync(TokenHashesKey(session.Id), ttl);
        _ = transaction.SetAddAsync(AccountKey(session.AccountId), session.Id.ToString("N"));
        _ = transaction.KeyExpireAsync(AccountKey(session.AccountId), TimeSpan.FromDays(30));
    }

    private RedisKey SessionKey(Guid id) => $"{options.Value.KeyPrefix}session:{id:N}";
    private RedisKey TokenHashesKey(Guid id) => $"{options.Value.KeyPrefix}session:{id:N}:tokens";
    private RedisKey RefreshKey(string hash) => $"{options.Value.KeyPrefix}refresh:{hash}";
    private RedisKey AccountKey(Guid id) => $"{options.Value.KeyPrefix}account:{id:N}:sessions";
}
