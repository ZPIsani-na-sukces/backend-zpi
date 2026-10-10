using System.Globalization;

namespace Zpi.Shared.Versioning;

public static class EntityTag
{
    public static string FromVersion(int version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static bool TryParseVersion(string value, out int version)
    {
        version = 0;
        var tag = value.Trim();
        return tag.Length >= 3 && tag[0] == '"' && tag[^1] == '"' &&
            int.TryParse(tag.AsSpan(1, tag.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out version) && version > 0;
    }
}
