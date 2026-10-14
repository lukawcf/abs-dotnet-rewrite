using System.Globalization;
using System.Text.Json;

namespace Abs.Api.Services;

// Finite scalar subset of JavaScript coercion; complex/non-finite values are unsupported.
internal static class ProgressPatchValues
{
    // Sequelize BOOLEAN sanitizes exact "true"/"false" strings and numeric 1/0.
    // Creation still uses JS truthiness; existing-row assignment uses this separate conversion.
    public static bool BooleanScalar(JsonElement value) => value.ValueKind is
        JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False ||
        (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && n is 0 or 1) ||
        (value.ValueKind == JsonValueKind.String && value.GetString() is "true" or "false");

    public static bool? StoredBoolean(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString() == "true",
        _ => Truth(value)
    };

    public static bool Truth(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.False => false,
        JsonValueKind.Number => v.GetDouble() != 0,
        JsonValueKind.String => v.GetString()!.Length > 0,
        _ => true
    };

    public static double? Number(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Null) return 0;
        if (v.ValueKind == JsonValueKind.True) return 1;
        if (v.ValueKind == JsonValueKind.False) return 0;
        if (v.ValueKind == JsonValueKind.Number)
            return v.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;
        if (v.ValueKind != JsonValueKind.String) return null;
        var s = v.GetString()!.Trim();
        if (s.Length == 0) return 0;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            ulong.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return hex;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    }

    public static DateTimeOffset? Date(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n))
        {
            try { return DateTimeOffset.FromUnixTimeMilliseconds(n); }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return v.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(v.GetString(),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;
    }
}
