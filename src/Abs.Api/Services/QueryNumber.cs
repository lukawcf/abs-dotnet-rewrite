using System.Globalization;

namespace Abs.Api.Services;

/// <summary>Query string number rules copied from Node's Express handlers, which coerce with Number().</summary>
public static class QueryNumber
{
    /// <summary>
    /// LibraryController.middleware (1486-1495): missing becomes 0, text that is not a number becomes 0,
    /// and anything that is not a non-negative integer is rejected (null here, 400 in the controller).
    /// </summary>
    public static int? ParsePaging(string? text)
    {
        if (text is null)
        {
            return 0;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return 0;
        }

        return number >= 0 && number == Math.Floor(number) && number <= int.MaxValue ? (int)number : null;
    }

    /// <summary>JavaScript <c>req.query.expanded == 1</c>.</summary>
    public static bool IsOne(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == 1;
}
