using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Abs.Data.Conversions;

/// <summary>
/// Node (Sequelize) stores dates as text like "2026-10-06 22:08:12.334 +00:00".
/// Shared by every entity that has a date column.
/// </summary>
public class AbsDateConverter() : ValueConverter<DateTimeOffset, string>(
    value => value.ToString(WriteFormat, CultureInfo.InvariantCulture),
    text => Parse(text))
{
    private const string WriteFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    private static readonly string[] ReadFormats =
    [
        "yyyy-MM-dd HH:mm:ss.fff zzz",
        "yyyy-MM-dd HH:mm:ss zzz",
    ];

    private static DateTimeOffset Parse(string text) =>
        DateTimeOffset.ParseExact(text, ReadFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
}
