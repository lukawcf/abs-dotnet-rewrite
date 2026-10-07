using Abs.Api.Abstractions;

namespace Abs.Api.Tests.Fakes;

/// <summary>In-memory progress rows keyed by user id and library item id.</summary>
public class FakeUserProgressReader : IUserProgressReader
{
    private readonly Dictionary<(string UserId, string LibraryItemId), UserProgress> rows = [];

    public FakeUserProgressReader Add(string userId, UserProgress progress)
    {
        rows[(userId, progress.LibraryItemId)] = progress;
        return this;
    }

    public Task<UserProgress?> GetProgressAsync(string userId, string libraryItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult(rows.GetValueOrDefault((userId, libraryItemId)));
}
