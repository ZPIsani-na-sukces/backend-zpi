namespace Zpi.Shared.Pagination;

public class CursorRequest
{
    public string? Cursor { get; set; }
    public int Limit { get; set; } = 20;
}
