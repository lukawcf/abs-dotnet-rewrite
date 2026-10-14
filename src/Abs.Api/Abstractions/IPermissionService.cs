namespace Abs.Api.Abstractions;

/// <summary>Answers permission questions for the current request's user.</summary>
public interface IPermissionService
{
    /// <summary>Same rule as Node's User.checkCanAccessLibrary: all-libraries flag, else the allow list.</summary>
    Task<bool> CanAccessLibraryAsync(string libraryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The user's librariesAccessible list. An empty list means "no restriction" for list endpoints
    /// (Node's GET /api/libraries quirk). Null means the user does not exist.
    /// </summary>
    Task<IReadOnlyList<string>?> GetLibrariesAccessibleAsync(CancellationToken cancellationToken = default);

    /// <summary>Explicit-content and tag rules of the current user. Null means the user does not exist.</summary>
    Task<ItemAccessRules?> GetItemAccessRulesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Item level restrictions from users.permissions (Node: User.canAccessExplicitContent,
/// User.checkCanAccessLibraryItemWithTags in server/models/User.js).
/// </summary>
public record ItemAccessRules(bool CanAccessExplicit, bool AccessAllTags, bool SelectedTagsNotAccessible, IReadOnlyList<string> SelectedTags)
{
    /// <summary>Node's list queries only filter by tags when this is true (libraryItemsBookFilters.js:30-45).</summary>
    public bool RestrictsListByTags => !AccessAllTags && SelectedTags.Count > 0;

    /// <summary>Node's single item check (User.js:658-682), without the library check.</summary>
    public bool CanAccessItem(bool isExplicit, IReadOnlyCollection<string> tags)
    {
        if (isExplicit && !CanAccessExplicit)
        {
            return false;
        }

        if (AccessAllTags)
        {
            return true;
        }

        if (SelectedTagsNotAccessible)
        {
            return tags.Count == 0 || tags.All(tag => !SelectedTags.Contains(tag));
        }

        return tags.Count != 0 && SelectedTags.Any(tags.Contains);
    }
}
