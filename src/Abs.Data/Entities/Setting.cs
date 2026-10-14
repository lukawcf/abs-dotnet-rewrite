namespace Abs.Data.Entities;

/// <summary>Table "settings". The row with key "server-settings" holds Node's ServerSettings as JSON text.</summary>
public class Setting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
