namespace Abs.Data.Entities;

/// <summary>Table "bookSeries". Series order on a book is createdAt ascending. Sequence is text such as "2.5".</summary>
public class BookSeries
{
    public required string Id { get; set; }
    public string? BookId { get; set; }
    public string? SeriesId { get; set; }
    public string? Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Series? Series { get; set; }
}
