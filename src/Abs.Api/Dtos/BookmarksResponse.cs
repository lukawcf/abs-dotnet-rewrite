using System.Text.Json;

namespace Abs.Api.Dtos;

// Node MeController.js:123-146: preserve each stored object's properties, order and JSON types.
public record BookmarksResponse(IReadOnlyList<JsonElement> Bookmarks);
