namespace Abs.Data.Entities;

/// <summary>Table "bookAuthors". Author order on a book is createdAt ascending.</summary>
public class BookAuthor
{
    public required string Id { get; set; }
    public string? BookId { get; set; }
    public string? AuthorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Author? Author { get; set; }
}
