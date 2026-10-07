namespace Abs.Api.Abstractions;

/// <summary>Progress of one user on one library item. Implemented by the Progress module.</summary>
public record UserProgress(string LibraryItemId, double Progress, double CurrentTime, bool IsFinished);

public interface IUserProgressReader
{
    /// <summary>Returns null when the user has no progress row for the item.</summary>
    Task<UserProgress?> GetProgressAsync(string userId, string libraryItemId, CancellationToken cancellationToken = default);
}
