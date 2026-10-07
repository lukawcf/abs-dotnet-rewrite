using System.Text.Json.Nodes;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

/// <summary>JSON shape of Node's Library.toOldJSON (server/models/Library.js). Property order matches Node.</summary>
public record LibraryDto(
    string Id,
    string? Name,
    IReadOnlyList<LibraryFolderDto> Folders,
    int? DisplayOrder,
    string? Icon,
    string? MediaType,
    string? Provider,
    JsonObject Settings,
    long? LastScan,
    string? LastScanVersion,
    long CreatedAt,
    long LastUpdate)
{
    public static LibraryDto From(Library library) => new(
        library.Id,
        library.Name,
        library.Folders.Select(LibraryFolderDto.From).ToList(),
        library.DisplayOrder,
        library.Icon,
        library.MediaType,
        library.Provider,
        ParseSettings(library.Settings),
        library.LastScan?.ToUnixTimeMilliseconds(),
        library.LastScanVersion,
        library.CreatedAt.ToUnixTimeMilliseconds(),
        library.UpdatedAt.ToUnixTimeMilliseconds());

    // Node spreads the stored JSON, so a missing value becomes {}.
    private static JsonObject ParseSettings(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
}

public record LibraryFolderDto(string Id, string? FullPath, string? LibraryId, long AddedAt)
{
    public static LibraryFolderDto From(LibraryFolder folder) =>
        new(folder.Id, folder.Path, folder.LibraryId, folder.CreatedAt.ToUnixTimeMilliseconds());
}

public record LibrariesResponse(IReadOnlyList<LibraryDto> Libraries);
