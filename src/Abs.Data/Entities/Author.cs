namespace Abs.Data.Entities;

/// <summary>Table "authors".</summary>
public class Author
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
