namespace Abs.Data.Entities;

/// <summary>Table "books". JSON columns stay as raw text and are parsed in the DTO.</summary>
public class Book
{
    public required string Id { get; set; }
    public string? Title { get; set; }
    public string? TitleIgnorePrefix { get; set; }
    public string? Subtitle { get; set; }
    public string? PublishedYear { get; set; }
    public string? PublishedDate { get; set; }
    public string? Publisher { get; set; }
    public string? Description { get; set; }
    public string? Isbn { get; set; }
    public string? Asin { get; set; }
    public string? Language { get; set; }
    public bool? Explicit { get; set; }
    public bool? Abridged { get; set; }
    public string? CoverPath { get; set; }
    public double? Duration { get; set; }

    /// <summary>JSON text array of strings.</summary>
    public string? Narrators { get; set; }

    /// <summary>JSON text array of audio file objects.</summary>
    public string? AudioFiles { get; set; }

    /// <summary>JSON text object, or null.</summary>
    public string? EbookFile { get; set; }

    /// <summary>JSON text array of chapter objects.</summary>
    public string? Chapters { get; set; }

    /// <summary>JSON text array of strings.</summary>
    public string? Tags { get; set; }

    /// <summary>JSON text array of strings.</summary>
    public string? Genres { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<BookAuthor> BookAuthors { get; set; } = [];
    public List<BookSeries> BookSeries { get; set; } = [];
}
