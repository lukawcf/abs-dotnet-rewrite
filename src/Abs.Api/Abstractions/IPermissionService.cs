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
}
