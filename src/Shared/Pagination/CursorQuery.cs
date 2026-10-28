using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;

namespace Zpi.Shared.Pagination;

public static class CursorQuery
{
    public static CursorQuery<T> Paginate<T>(this IQueryable<T> source, string? cursor, object scope) => new(source, cursor, scope);

    public static bool IsValid(string? cursor)
    {
        try
        {
            Decode(cursor);
            return true;
        }
        catch (ValidationException)
        {
            return false;
        }
    }

    internal static CursorState? Decode(string? cursor)
    {
        if (cursor is null) return null;
        if (cursor.Length is < 1 or > 4096 || cursor.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            throw Invalid();
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var state = JsonSerializer.Deserialize<CursorState>(Convert.FromBase64String(base64));
            if (state is null || state.Version != 1 || state.Scope?.Length != 64 || state.Keys is not { Length: > 0 and <= 3 })
                throw Invalid();
            return state;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            throw Invalid();
        }
    }

    internal static ValidationException Invalid() => new([new ValidationFailure("Cursor", "Invalid cursor for this list or its filters.")]);
}

internal sealed record CursorState(int Version, string Scope, JsonElement[] Keys);

public sealed class CursorQuery<T>
{
    private readonly IQueryable<T> source;
    private readonly CursorState? cursor;
    private readonly string scope;
    private readonly ParameterExpression parameter = Expression.Parameter(typeof(T), "row");
    private readonly List<Func<T, JsonElement>> keys = [];
    private IOrderedQueryable<T>? ordered;
    private Expression prefix = Expression.Constant(true);
    private Expression after = Expression.Constant(false);

    internal CursorQuery(IQueryable<T> source, string? cursor, object scope)
    {
        this.source = source;
        this.scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
        this.cursor = CursorQuery.Decode(cursor);
        if (this.cursor is not null && this.cursor.Scope != this.scope) throw CursorQuery.Invalid();
    }

    public CursorQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key, bool descending = false) where TKey : IComparable<TKey>
    {
        ordered = ordered is null
            ? descending ? source.OrderByDescending(key) : source.OrderBy(key)
            : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);
        var getter = key.Compile();
        if (cursor is not null)
        {
            if (keys.Count >= cursor.Keys.Length) throw CursorQuery.Invalid();
            TKey? value;
            try
            {
                value = cursor.Keys[keys.Count].Deserialize<TKey>();
            }
            catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
            {
                throw CursorQuery.Invalid();
            }
            if (value is null) throw CursorQuery.Invalid();
            var member = new ReplaceParameter(key.Parameters[0], parameter).Visit(key.Body)!;
            Expression<Func<TKey>> capturedValue = () => value!;
            var constant = capturedValue.Body;
            var comparison = Expression.Call(member, typeof(TKey).GetMethod(nameof(IComparable<TKey>.CompareTo), [typeof(TKey)])!, constant);
            var beyond = descending ? Expression.LessThan(comparison, Expression.Constant(0))
                : Expression.GreaterThan(comparison, Expression.Constant(0));
            after = Expression.OrElse(after, Expression.AndAlso(prefix, beyond));
            prefix = Expression.AndAlso(prefix, Expression.Equal(member, constant));
        }
        keys.Add(row => JsonSerializer.SerializeToElement(getter(row)));
        return this;
    }

    public IQueryable<T> Take(int limit)
    {
        if (ordered is null || limit is < 1 or > 100) throw CursorQuery.Invalid();
        if (cursor is not null && cursor.Keys.Length != keys.Count) throw CursorQuery.Invalid();
        return (cursor is null ? ordered : ordered.Where(Expression.Lambda<Func<T, bool>>(after, parameter))).Take(limit + 1);
    }

    public string? NextCursor(IReadOnlyList<T> rows, int limit)
    {
        if (rows.Count <= limit) return null;
        var state = new CursorState(1, scope, keys.Select(key => key(rows[limit - 1])).ToArray());
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class ReplaceParameter(ParameterExpression original, ParameterExpression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == original ? replacement : base.VisitParameter(node);
    }
}
