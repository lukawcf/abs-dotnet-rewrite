namespace Abs.Data.Entities;

/// <summary>
/// Table "users". Auth and Users module owns this entity.
/// Node owns the schema — .NET only reads.
/// </summary>
public class User
{
    public required string Id { get; set; }
    public string? Username { get; set; }
    public string? Type { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>JSON text, e.g. {"accessAllLibraries":true,"librariesAccessible":[]}.</summary>
    public string? Permissions { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
