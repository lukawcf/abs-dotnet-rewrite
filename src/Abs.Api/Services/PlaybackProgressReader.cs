using Abs.Api.Abstractions;

namespace Abs.Api.Services;

public partial class PlaybackProgressService
{
    // Adapter for the existing internal contract. Node User.js:716-721 and
    // MediaProgress.js:155-178 define the lookup and stored progress used by GetAsync.
    // The caller supplies an authorized userId; this method does not inspect HTTP context.
    public async Task<UserProgress?> GetProgressAsync(
        string userId, string libraryItemId, CancellationToken cancellationToken = default)
    {
        var progress = await GetAsync(userId, libraryItemId, null, cancellationToken);
        return progress is null ? null : new UserProgress(libraryItemId,
            ProgressPatchValues.Number(progress.Progress) ?? 0,
            progress.CurrentTime ?? 0, progress.IsFinished);
    }
}
