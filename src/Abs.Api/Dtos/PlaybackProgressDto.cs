using System.Text.Json;
using Abs.Data.Entities;

namespace Abs.Api.Dtos;

// Node v2.37.1 server/models/MediaProgress.js:155-178 (getOldMediaProgress).
public record PlaybackProgressDto(
    string? Id, string? UserId, JsonElement? LibraryItemId, string? EpisodeId,
    string? MediaItemId, string? MediaItemType, double? Duration, JsonElement Progress,
    double? CurrentTime, bool IsFinished, bool HideFromContinueListening,
    string? EbookLocation, double? EbookProgress, long LastUpdate, long StartedAt, long? FinishedAt)
{
    public static PlaybackProgressDto From(PlaybackProgress row)
    {
        using var json = JsonDocument.Parse(row.ExtraData ?? "{}");
        var root = json.RootElement;
        JsonElement? Get(string name) => root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(name, out var value) ? value.Clone() : null;
        var item = Get("libraryItemId");
        var progress = Get("progress");
        return new(row.Id, row.UserId, Truthy(item) ? item : null,
            row.MediaItemType == "podcastEpisode" ? row.MediaItemId : null,
            row.MediaItemId, row.MediaItemType, row.Duration,
            Truthy(progress) ? progress!.Value : JsonSerializer.SerializeToElement(0),
            row.CurrentTime, row.IsFinished ?? false, row.HideFromContinueListening ?? false,
            row.EbookLocation, row.EbookProgress, row.UpdatedAt.ToUnixTimeMilliseconds(),
            row.CreatedAt.ToUnixTimeMilliseconds(),
            row.FinishedAt?.ToUnixTimeMilliseconds() is { } finished && finished != 0 ? finished : null);
    }

    private static bool Truthy(JsonElement? value) => value is { } v && v.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.False => false,
        JsonValueKind.String => v.GetString()!.Length > 0,
        JsonValueKind.Number => v.GetDouble() != 0,
        _ => true
    };
}
