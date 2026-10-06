using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Zpi.Database;

public sealed class CamelCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()),
    value => Enum.Parse<TEnum>(value, true)) where TEnum : struct, Enum;
