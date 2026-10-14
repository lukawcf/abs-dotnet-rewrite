namespace Abs.Data.Entities;

/// <summary>Table "series".</summary>
public class Series
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
