namespace Abs.Data.Entities;

/// <summary>Table "libraryFolders".</summary>
public class LibraryFolder
{
    public required string Id { get; set; }
    public string? Path { get; set; }
    public string? LibraryId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Library? Library { get; set; }
}
