namespace Abs.Data.Entities;

/// <summary>
/// Table "users", minimal mapping: only what permission checks need.
/// The Auth and Users module owns this entity and extends it there.
/// </summary>
public class User
{
    public required string Id { get; set; }
    public string? Username { get; set; }
    public string? Type { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>JSON text, e.g. {"accessAllLibraries":true,"librariesAccessible":[]}.</summary>
    public string? Permissions { get; set; }
}
