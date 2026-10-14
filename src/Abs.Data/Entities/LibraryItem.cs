namespace Abs.Data.Entities;

/// <summary>Table "libraryItems". mediaId points at books.id when mediaType is "book" (no foreign key in the database).</summary>
public class LibraryItem
{
    public required string Id { get; set; }
    public string? Ino { get; set; }
    public string? Path { get; set; }
    public string? RelPath { get; set; }
    public string? MediaId { get; set; }
    public string? MediaType { get; set; }
    public bool? IsFile { get; set; }
    public bool? IsMissing { get; set; }
    public bool? IsInvalid { get; set; }
    public DateTimeOffset? Mtime { get; set; }
    public DateTimeOffset? Ctime { get; set; }
    public DateTimeOffset? Birthtime { get; set; }
    public long? Size { get; set; }
    public DateTimeOffset? LastScan { get; set; }
    public string? LastScanVersion { get; set; }

    /// <summary>JSON text array. Node passes it through unchanged.</summary>
    public string? LibraryFiles { get; set; }

    /// <summary>JSON text, e.g. {"oldLibraryItemId":"li_x"}.</summary>
    public string? ExtraData { get; set; }

    public string? Title { get; set; }
    public string? TitleIgnorePrefix { get; set; }
    public string? AuthorNamesFirstLast { get; set; }
    public string? AuthorNamesLastFirst { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? LibraryId { get; set; }
    public string? LibraryFolderId { get; set; }

    public Book? Book { get; set; }
}
