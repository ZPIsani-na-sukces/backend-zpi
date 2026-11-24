using System.Text.Json;
using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.IdentityTests;

public sealed class CursorQueryTests
{
    private static readonly Row[] Rows = [new("A", 3), new("A", 1), new("B", 4), new("B", 2)];

    [Theory]
    [InlineData(false, true, 3, 1, 4, 2)]
    [InlineData(true, false, 2, 4, 1, 3)]
    public void Cursor_continues_mixed_sort_order_after_boundary_is_removed(bool mainDescending, bool tieDescending,
        int first, int second, int third, int fourth)
    {
        var initial = Sort(Rows, null, mainDescending, tieDescending);
        var page = initial.Take(1).ToArray();
        Assert.Equal(new[] { first, second }, page.Select(r => r.Id));
        var cursor = initial.NextCursor(page, 1);
        Assert.NotNull(cursor);

        var remaining = Rows.Where(row => row.Id != first);
        var next = Sort(remaining, cursor, mainDescending, tieDescending);
        var finalPage = next.Take(3).ToArray();
        Assert.Equal(new[] { second, third, fourth }, finalPage.Select(r => r.Id));
        Assert.Null(next.NextCursor(finalPage, 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid+=")]
    [InlineData("A")]
    [InlineData("e30")]
    [InlineData("bnVsbA")]
    public void Malformed_cursors_are_rejected_before_querying(string input)
    {
        Assert.False(CursorQuery.IsValid(input));
        Assert.Throws<ValidationException>(() => Rows.AsQueryable().Paginate(input, "account-scope"));
    }

    [Fact]
    public void Cursor_is_bound_to_scope_key_count_and_key_types()
    {
        var query = Sort(Rows, null, false, false);
        var page = query.Take(1).ToArray();
        var token = query.NextCursor(page, 1)!;

        Assert.True(CursorQuery.IsValid(token));
        Assert.Throws<ValidationException>(() => Rows.AsQueryable().Paginate(token, "another-account"));
        Assert.Throws<ValidationException>(() => Rows.AsQueryable().Paginate(token, "account-scope")
            .OrderBy(row => row.Group).Take(1));
        Assert.Throws<ValidationException>(() => Sort(Rows, token, false, false).OrderBy(row => row.Id));
        Assert.Throws<ValidationException>(() => Rows.AsQueryable().Paginate(token, "account-scope").OrderBy(row => row.Id));

        var value = JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(
            token.Replace('-', '+').Replace('_', '/').PadRight((token.Length + 3) / 4 * 4, '=')));
        var nullKey = Encode(new { Version = 1, Scope = value.GetProperty("Scope").GetString(), Keys = new string?[] { null } });
        Assert.Throws<ValidationException>(() => Rows.AsQueryable().Paginate(nullKey, "account-scope").OrderBy(row => row.Group));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Pagination_rejects_limits_outside_contract(int limit)
    {
        Assert.Throws<ValidationException>(() => Sort(Rows, null, false, false).Take(limit));
    }

    [Fact]
    public void Unordered_query_cannot_publish_a_cursor_page()
    {
        Assert.Throws<ValidationException>(() => Rows.AsQueryable().Paginate(null, "account-scope").Take(1));
    }

    private static CursorQuery<Row> Sort(IEnumerable<Row> rows, string? cursor, bool mainDescending, bool tieDescending) =>
        rows.AsQueryable().Paginate(cursor, "account-scope").OrderBy(row => row.Group, mainDescending)
            .OrderBy(row => row.Id, tieDescending);

    private static string Encode<T>(T value) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record Row(string Group, int Id);
}
