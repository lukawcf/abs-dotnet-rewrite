using System.Text.Json.Serialization;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

/// <summary>
/// JSON shape of Node's LibraryItem.toOldJSONMinified for a book (server/models/LibraryItem.js:1000-1025),
/// the element type of GET /api/libraries/:id/items results. Property order matches Node.
/// </summary>
public record LibraryItemMinifiedDto(
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
    bool IsMissing,
    bool IsInvalid,
    string? MediaType,
    BookMinifiedDto Media,
    int NumFiles,
    long? Size,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RssFeedDto? RssFeed = null)
{
    public static LibraryItemMinifiedDto From(LibraryItem item, Book book, ServerSortSettings settings)
    {
        var media = BookMinifiedDto.From(book, item.Size, item.AuthorNamesLastFirst, settings);
        return new(
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
            item.IsMissing == true,
            item.IsInvalid == true,
            item.MediaType,
            media,
            ItemJson.Count(ItemJson.Parse(item.LibraryFiles)),
            item.Size);
    }
}

/// <summary>Node's Feed.toOldJSONMinified (server/models/Feed.js), added to an item by ?include=rssfeed.</summary>
public record RssFeedDto(string Id, string? EntityType, string? EntityId, string? FeedUrl, RssFeedMetaDto Meta)
{
    public static RssFeedDto From(Feed feed) => new(
        feed.Id,
        feed.EntityType,
        feed.EntityId,
        feed.FeedUrl,
        new RssFeedMetaDto(feed.Title, feed.Description, feed.PreventIndexing, feed.OwnerName, feed.OwnerEmail));
}

public record RssFeedMetaDto(string? Title, string? Description, bool? PreventIndexing, string? OwnerName, string? OwnerEmail);

/// <summary>Node's Book.toOldJSONMinified (server/models/Book.js:640-659).</summary>
public record BookMinifiedDto(
    string Id,
    BookMinifiedMetadataDto Metadata,
    string? CoverPath,
    IReadOnlyList<string> Tags,
    int NumTracks,
    int NumAudioFiles,
    int NumChapters,
    double? Duration,
    long Size,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EbookFormat)
{
    public static BookMinifiedDto From(Book book, long? itemSize, string? authorNameLastFirst, ServerSortSettings settings)
    {
        var audioFiles = ItemJson.Parse(book.AudioFiles);
        var ebookFile = ItemJson.Parse(book.EbookFile);
        var size = ItemJson.BookSize(audioFiles, ebookFile);

        // LibraryItem.getByFilterAndSort fills an empty media size from the item size (server/models/LibraryItem.js:318-320).
        if (size == 0 && itemSize is > 0)
        {
            size = itemSize.Value;
        }

        return new(
            book.Id,
            BookMinifiedMetadataDto.From(book, authorNameLastFirst, settings),
            book.CoverPath,
            ItemJson.StringList(book.Tags),
            ItemJson.NumTracks(audioFiles),
            ItemJson.Count(audioFiles),
            ItemJson.Count(ItemJson.Parse(book.Chapters)),
            book.Duration,
            size,
            ItemJson.EbookFormat(ebookFile));
    }
}

/// <summary>Node's Book.oldMetadataToJSONMinified (server/models/Book.js:565-585).</summary>
public record BookMinifiedMetadataDto(
    string? Title,
    string? TitleIgnorePrefix,
    string? Subtitle,
    string AuthorName,
    string AuthorNameLF,
    string NarratorName,
    string SeriesName,
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
    // authorNameLF comes from libraryItems.authorNamesLastFirst, which Node fills with the same
    // nameToLastFirst call; re-implementing Node's name parser here is not worth the drift risk.
    public static BookMinifiedMetadataDto From(Book book, string? authorNameLastFirst, ServerSortSettings settings) => new(
        book.Title,
        ItemJson.TitlePrefixAtEnd(book.Title, settings.SortingPrefixes),
        book.Subtitle,
        ItemJson.AuthorName(book),
        authorNameLastFirst ?? string.Empty,
        string.Join(", ", ItemJson.StringList(book.Narrators)),
        ItemJson.SeriesName(book),
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

public record LibraryItemsResponse(
    IReadOnlyList<LibraryItemMinifiedDto> Results,
    int Total,
    int Limit,
    int Page,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SortBy,
    bool SortDesc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FilterBy,
    string? MediaType,
    bool Minified,
    bool Collapseseries,
    string Include,
    int Offset);
