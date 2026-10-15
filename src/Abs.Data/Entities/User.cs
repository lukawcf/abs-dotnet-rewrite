namespace Abs.Data.Entities;

/// <summary>
/// Table "users". JSON columns (permissions, extraData) stay raw text. The bookmarks column is read through a
/// shadow property (see BookmarkUserConfiguration). Never map the password hash (pash).
/// </summary>
public class User
{
    public required string Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Type { get; set; }

    /// <summary>Old non-expiring API token that Node stores on the user row.</summary>
    public string? Token { get; set; }

    public bool? IsActive { get; set; }
    public bool? IsLocked { get; set; }
    public DateTimeOffset? LastSeen { get; set; }

    /// <summary>JSON text, e.g. {"accessAllLibraries":true,"librariesAccessible":[]}.</summary>
    public string? Permissions { get; set; }

    /// <summary>JSON text, e.g. {"seriesHideFromContinueListening":[]}.</summary>
    public string? ExtraData { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
