using System.Text.Json;

namespace Zpi.Domain;

public static class EnumExtensions
{
    public static string ToCamelCase<TEnum>(this TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
