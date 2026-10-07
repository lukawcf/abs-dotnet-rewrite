using Abs.Api.Abstractions;

namespace Abs.Api.Tests.Fakes;

/// <summary>Fixed answers, for tests that only care about how a controller reacts.</summary>
public class FakePermissionService(bool canAccess, IReadOnlyList<string>? librariesAccessible) : IPermissionService
{
    public Task<bool> CanAccessLibraryAsync(string libraryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(canAccess);

    public Task<IReadOnlyList<string>?> GetLibrariesAccessibleAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(librariesAccessible);
}
