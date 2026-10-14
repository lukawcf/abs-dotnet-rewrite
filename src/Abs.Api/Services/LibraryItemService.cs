using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Abs.Api.Dtos;
using Abs.Data;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

/// <summary>Sorts of GET /api/libraries/:id/items that this service can reproduce from the database alone.</summary>
public enum ItemSort
{
    /// <summary>Node applies no ORDER BY. Stable paging needs one, so createdAt is used.</summary>
    None,
    AddedAt,
    Size,
    BirthtimeMs,
    MtimeMs,
    Duration,
    PublishedYear,
    AuthorName,
    AuthorNameLastFirst,
    Title,
}

/// <summary>Result of looking up one item. Item is null when the item is not a book.</summary>
public record ItemLookup(string? MediaType, LibraryItemDto? Item);

public class LibraryItemService(AbsDbContext db)
{
    /// <summary>Maps Node's sort query value (libraryItemsBookFilters.js:254-300). Null means this service cannot sort that way.</summary>
    public static ItemSort? ParseSort(string? sort) => sort switch
    {
        null or "" => ItemSort.None,
        "addedAt" => ItemSort.AddedAt,
        "size" => ItemSort.Size,
        "birthtimeMs" => ItemSort.BirthtimeMs,
        "mtimeMs" => ItemSort.MtimeMs,
        "media.duration" => ItemSort.Duration,
        "media.metadata.publishedYear" => ItemSort.PublishedYear,
        "media.metadata.authorName" => ItemSort.AuthorName,
        "media.metadata.authorNameLF" => ItemSort.AuthorNameLastFirst,
        // Without a series filter Node replaces "sequence" with the title sort (libraryItemsBookFilters.js:404-406).
        "media.metadata.title" or "sequence" => ItemSort.Title,
        _ => null,
    };

    /// <summary>Null when there is no item or the book row is missing (Node answers 404 for both).</summary>
    public async Task<ItemLookup?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await db.LibraryItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Book).ThenInclude(x => x!.BookAuthors.OrderBy(a => a.CreatedAt)).ThenInclude(x => x.Author)
            .Include(x => x.Book).ThenInclude(x => x!.BookSeries.OrderBy(s => s.CreatedAt)).ThenInclude(x => x.Series)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.MediaType != "book")
        {
            return new ItemLookup(item.MediaType, null);
        }

        return item.Book is null ? null : new ItemLookup(item.MediaType, LibraryItemDto.From(item, item.Book));
    }

    /// <summary>
    /// One page of a book library. limit 0 means everything. Items without a book row are left out, as
    /// Node's inner join does. Hides explicit books when the user may not see them.
    /// </summary>
    public async Task<(IReadOnlyList<LibraryItemMinifiedDto> Items, int Total)> GetPageAsync(
        string libraryId, ItemSort sort, bool desc, int limit, int offset, bool canAccessExplicit,
        CancellationToken cancellationToken = default)
    {
        var settings = await LoadSortSettingsAsync(cancellationToken);

        var query = db.LibraryItems.Where(x => x.LibraryId == libraryId && x.MediaType == "book" && x.Book != null);
        if (!canAccessExplicit)
        {
            query = query.Where(x => x.Book!.Explicit == false);
        }

        var total = await query.CountAsync(cancellationToken);
        var ids = await GetPageIdsAsync(query, sort, desc, limit, offset, settings, cancellationToken);
        if (ids.Count == 0)
        {
            return ([], total);
        }

        var items = await db.LibraryItems.AsSplitQuery()
            .Include(x => x.Book).ThenInclude(x => x!.BookAuthors.OrderBy(a => a.CreatedAt)).ThenInclude(x => x.Author)
            .Include(x => x.Book).ThenInclude(x => x!.BookSeries.OrderBy(s => s.CreatedAt)).ThenInclude(x => x.Series)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var position = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var dtos = items
            .OrderBy(x => position[x.Id])
            .Select(x => LibraryItemMinifiedDto.From(x, x.Book!, settings))
            .ToList();
        return (dtos, total);
    }

    private async Task<List<string>> GetPageIdsAsync(
        IQueryable<LibraryItem> query, ItemSort sort, bool desc, int limit, int offset,
        ServerSortSettings settings, CancellationToken cancellationToken)
    {
        if (sort == ItemSort.PublishedYear)
        {
            return await GetPublishedYearPageIdsAsync(query, desc, limit, offset, cancellationToken);
        }

        var ordered = Order(query, sort, desc, settings).ThenBy(x => x.Id).Select(x => x.Id);
        if (offset > 0)
        {
            ordered = ordered.Skip(offset);
        }

        if (limit > 0)
        {
            ordered = ordered.Take(limit);
        }

        return await ordered.ToListAsync(cancellationToken);
    }

    // Node sorts CAST(publishedYear AS INTEGER). LINQ has no CAST to integer, and a text sort would put
    // "999" after "1937", so this one sort runs in memory over (id, year) pairs.
    private static async Task<List<string>> GetPublishedYearPageIdsAsync(
        IQueryable<LibraryItem> query, bool desc, int limit, int offset, CancellationToken cancellationToken)
    {
        var rows = await query.Select(x => new { x.Id, x.Book!.PublishedYear }).ToListAsync(cancellationToken);
        var keyed = rows.Select(x => (x.Id, Year: LeadingInteger(x.PublishedYear)));
        var ordered = desc
            ? keyed.OrderByDescending(x => x.Year).ThenBy(x => x.Id, StringComparer.Ordinal)
            : keyed.OrderBy(x => x.Year).ThenBy(x => x.Id, StringComparer.Ordinal);
        var page = ordered.Select(x => x.Id).Skip(offset);
        return (limit > 0 ? page.Take(limit) : page).ToList();
    }

    /// <summary>SQLite CAST(text AS INTEGER): leading digits, 0 when there are none, NULL stays NULL (smallest).</summary>
    private static long? LeadingInteger(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.TrimStart();
        var end = 0;
        if (end < trimmed.Length && trimmed[end] is '+' or '-')
        {
            end++;
        }

        var digitsStart = end;
        while (end < trimmed.Length && char.IsAsciiDigit(trimmed[end]))
        {
            end++;
        }

        return end > digitsStart && long.TryParse(trimmed[..end], out var value) ? value : 0;
    }

    private static IOrderedQueryable<LibraryItem> Order(
        IQueryable<LibraryItem> query, ItemSort sort, bool desc, ServerSortSettings settings)
    {
        Expression<Func<LibraryItem, string?>> title = settings.SortingIgnorePrefix
            ? x => EF.Functions.Collate(x.TitleIgnorePrefix, "NOCASE")
            : x => EF.Functions.Collate(x.Title, "NOCASE");

        return sort switch
        {
            ItemSort.AddedAt => By(query, x => x.CreatedAt, desc),
            ItemSort.Size => By(query, x => x.Size, desc),
            ItemSort.BirthtimeMs => By(query, x => x.Birthtime, desc),
            ItemSort.MtimeMs => By(query, x => x.Mtime, desc),
            ItemSort.Duration => By(query, x => x.Book!.Duration, desc),
            ItemSort.AuthorName => ThenBy(By(query, x => EF.Functions.Collate(x.AuthorNamesFirstLast, "NOCASE"), desc), title, desc),
            ItemSort.AuthorNameLastFirst => ThenBy(By(query, x => EF.Functions.Collate(x.AuthorNamesLastFirst, "NOCASE"), desc), title, desc),
            ItemSort.Title => By(query, title, desc),
            _ => By(query, x => x.CreatedAt, false),
        };
    }

    private static IOrderedQueryable<T> By<T, TKey>(IQueryable<T> query, Expression<Func<T, TKey>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);

    private static IOrderedQueryable<T> ThenBy<T, TKey>(IOrderedQueryable<T> query, Expression<Func<T, TKey>> key, bool desc) =>
        desc ? query.ThenByDescending(key) : query.ThenBy(key);

    private async Task<ServerSortSettings> LoadSortSettingsAsync(CancellationToken cancellationToken)
    {
        var json = await db.Settings.Where(x => x.Key == "server-settings").Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (ItemJson.Parse(json) is not JsonObject settings)
        {
            return ServerSortSettings.Default;
        }

        var prefixes = settings["sortingPrefixes"] is JsonArray array
            ? array.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList()
            : ["the"];
        return new ServerSortSettings(settings["sortingIgnorePrefix"]?.GetValue<bool>() == true, prefixes);
    }
}
