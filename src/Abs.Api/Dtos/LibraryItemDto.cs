using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

/// <summary>
/// JSON shape of Node's LibraryItem.toOldJSON for a book (server/models/LibraryItem.js:965-990).
/// Used by GET /api/items/:id without expanded=1. Property order matches Node. Properties Node leaves
/// undefined (so JSON.stringify drops them) are ignored when null.
/// </summary>
public record LibraryItemDto(
    string Id,
    string? Ino,
    string? OldLibraryItemId,
    string? LibraryId,
    string? FolderId,
    string? Path,
    string? RelPath,
    bool? IsFile,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MtimeMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? CtimeMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? BirthtimeMs,
    long AddedAt,
    long UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? LastScan,
    string? ScanVersion,
    bool IsMissing,
    bool IsInvalid,
    string? MediaType,
    BookDto Media,
    JsonNode? LibraryFiles)
{
    public static LibraryItemDto From(LibraryItem item, Book book) => new(
        item.Id,
        item.Ino,
        ItemJson.OldLibraryItemId(item.ExtraData),
        item.LibraryId,
        item.LibraryFolderId,
        item.Path,
        item.RelPath,
        item.IsFile,
        ItemJson.Ms(item.Mtime),
        ItemJson.Ms(item.Ctime),
        ItemJson.Ms(item.Birthtime),
        item.CreatedAt.ToUnixTimeMilliseconds(),
        item.UpdatedAt.ToUnixTimeMilliseconds(),
        ItemJson.Ms(item.LastScan),
        item.LastScanVersion,
        item.IsMissing == true,
        item.IsInvalid == true,
        item.MediaType,
        BookDto.From(item.Id, book),
        ItemJson.Parse(item.LibraryFiles));
}

/// <summary>Node's Book.toOldJSON (server/models/Book.js:615-634).</summary>
public record BookDto(
    string Id,
    string LibraryItemId,
    BookMetadataDto Metadata,
    string? CoverPath,
    IReadOnlyList<string> Tags,
    JsonNode? AudioFiles,
    JsonNode? Chapters,
    JsonNode? EbookFile)
{
    public static BookDto From(string libraryItemId, Book book) => new(
        book.Id,
        libraryItemId,
        BookMetadataDto.From(book),
        book.CoverPath,
        ItemJson.StringList(book.Tags),
        ItemJson.Parse(book.AudioFiles),
        ItemJson.Parse(book.Chapters),
        ItemJson.Parse(book.EbookFile));
}

/// <summary>Node's Book.oldMetadataToJSON (server/models/Book.js:544-563).</summary>
public record BookMetadataDto(
    string? Title,
    string? Subtitle,
    IReadOnlyList<AuthorRefDto> Authors,
    IReadOnlyList<string> Narrators,
    IReadOnlyList<SeriesRefDto> Series,
    IReadOnlyList<string> Genres,
    string? PublishedYear,
    string? PublishedDate,
    string? Publisher,
    string? Description,
    string? Isbn,
    string? Asin,
    string? Language,
    bool? Explicit,
    bool? Abridged)
{
    public static BookMetadataDto From(Book book) => new(
        book.Title,
        book.Subtitle,
        book.BookAuthors.Select(x => new AuthorRefDto(x.AuthorId!, x.Author?.Name)).ToList(),
        ItemJson.StringList(book.Narrators),
        book.BookSeries.Select(x => new SeriesRefDto(x.SeriesId!, x.Series?.Name, x.Sequence)).ToList(),
        ItemJson.StringList(book.Genres),
        book.PublishedYear,
        book.PublishedDate,
        book.Publisher,
        book.Description,
        book.Isbn,
        book.Asin,
        book.Language,
        book.Explicit,
        book.Abridged);
}

public record AuthorRefDto(string Id, string? Name);

public record SeriesRefDto(string Id, string? Name, string? Sequence);
