using System.Text.Json.Nodes;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

/// <summary>Conversions shared by the library item DTOs. Mirrors helpers in Node's server/models/Book.js and LibraryItem.js.</summary>
public static class ItemJson
{
    public static long? Ms(DateTimeOffset? value) => value?.ToUnixTimeMilliseconds();

    /// <summary>Parses a JSON text column; null stays null (Node returns null for an empty JSON column).</summary>
    public static JsonNode? Parse(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);

    public static IReadOnlyList<string> StringList(string? json) =>
        Parse(json) is JsonArray array
            ? array.Select(x => x?.GetValue<string>()).Where(x => x is not null).Select(x => x!).ToList()
            : [];

    public static string? OldLibraryItemId(string? extraDataJson) =>
        Parse(extraDataJson) is JsonObject extra && extra["oldLibraryItemId"]?.GetValue<string>() is { Length: > 0 } id ? id : null;

    /// <summary>Book.audioFiles entries that are not excluded (Book.includedAudioFiles).</summary>
    public static int NumTracks(JsonNode? audioFiles) =>
        audioFiles is JsonArray array ? array.Count(x => x?["exclude"]?.GetValue<bool>() != true) : 0;

    public static int Count(JsonNode? array) => array is JsonArray items ? items.Count : 0;

    /// <summary>Book.size: audio file sizes plus the ebook file size (server/models/Book.js:330-337).</summary>
    public static long BookSize(JsonNode? audioFiles, JsonNode? ebookFile)
    {
        double total = 0;
        if (audioFiles is JsonArray array)
        {
            total += array.Sum(x => x?["metadata"]?["size"]?.GetValue<double>() ?? 0);
        }

        total += ebookFile?["metadata"]?["size"]?.GetValue<double>() ?? 0;
        return (long)total;
    }

    public static string? EbookFormat(JsonNode? ebookFile) => ebookFile?["ebookFormat"]?.GetValue<string>();

    /// <summary>Node's getTitlePrefixAtEnd (server/utils/index.js:182-213): "The Hobbit" becomes "Hobbit, The".</summary>
    public static string? TitlePrefixAtEnd(string? title, IReadOnlyList<string> sortingPrefixes)
    {
        if (string.IsNullOrEmpty(title))
        {
            return title;
        }

        foreach (var prefix in sortingPrefixes)
        {
            if (title.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = title[(prefix.Length + 1)..];
                return $"{rest}, {char.ToUpperInvariant(prefix[0])}{prefix[1..]}";
            }
        }

        return title;
    }

    /// <summary>Book.authorName (comma separated, in bookAuthors.createdAt order).</summary>
    public static string AuthorName(Book book) =>
        string.Join(", ", book.BookAuthors.Select(x => x.Author?.Name));

    /// <summary>Book.seriesName: "Name #sequence" per series, comma separated.</summary>
    public static string SeriesName(Book book) =>
        string.Join(", ", book.BookSeries.Select(x =>
            string.IsNullOrEmpty(x.Sequence) ? x.Series?.Name : $"{x.Series?.Name} #{x.Sequence}"));
}

/// <summary>Server settings that change how items are sorted and printed (settings row "server-settings").</summary>
public record ServerSortSettings(bool SortingIgnorePrefix, IReadOnlyList<string> SortingPrefixes)
{
    /// <summary>What Node uses when the settings row does not exist (ServerSettings.js:73-74).</summary>
    public static readonly ServerSortSettings Default = new(false, ["the", "a"]);
}
