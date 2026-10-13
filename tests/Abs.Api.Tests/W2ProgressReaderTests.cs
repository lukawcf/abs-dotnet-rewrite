using System.Net;
using System.Net.Http.Json;
using Abs.Api.Abstractions;
using Abs.Api.Services;
using Abs.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Abs.Api.Tests;

public class W2ProgressReaderTests
{
    [Theory]
    [InlineData("w2-alice", "w2-item", 0.4, 50, false)]
    [InlineData("w2-alice", "w2-podcast", 0.25, 0, false)]
    [InlineData("w2-bob", "w2-private-item", 0, 100, true)]
    public async Task Reads_stored_progress_with_internal_numeric_projection(
        string userId, string itemId, double progress, double time, bool finished)
    {
        using var factory = new AbsApiFactory();
        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IUserProgressReader>();
        var actual = await reader.GetProgressAsync(userId, itemId);
        Assert.Equal(new UserProgress(itemId, progress, time, finished), actual);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AbsDbContext>().ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("w2-alice", "absent")]
    [InlineData("w2-alice", "w2-private-item")]
    [InlineData("w2-bob", "w2-item")]
    [InlineData("unknown", "w2-item")]
    public async Task Missing_or_other_users_progress_returns_null(string userId, string itemId)
    {
        using var factory = new AbsApiFactory();
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IUserProgressReader>()
            .GetProgressAsync(userId, itemId));
    }

    [Fact]
    public void Internal_reader_and_HTTP_service_share_one_scoped_instance()
    {
        using var factory = new AbsApiFactory();
        using var scope = factory.Services.CreateScope();
        Assert.Same(scope.ServiceProvider.GetRequiredService<PlaybackProgressService>(),
            scope.ServiceProvider.GetRequiredService<IUserProgressReader>());
    }

    [Fact]
    public async Task Existing_reader_observes_later_API_update()
    {
        using var factory = new AbsApiFactory { EnableProgressWrites = true };
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-User", "w2-alice");
        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IUserProgressReader>();
        Assert.Equal(50, (await reader.GetProgressAsync("w2-alice", "w2-item"))!.CurrentTime);
        var response = await client.PatchAsJsonAsync("/api/me/progress/w2-item", new { currentTime = 80 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(80, (await reader.GetProgressAsync("w2-alice", "w2-item"))!.CurrentTime);
        Assert.Null(await reader.GetProgressAsync("w2-bob", "w2-item"));
    }

    [Fact]
    public async Task Cancelled_request_is_not_converted_to_missing_progress()
    {
        using var factory = new AbsApiFactory();
        using var scope = factory.Services.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IUserProgressReader>()
                .GetProgressAsync("w2-alice", "w2-item", cancellation.Token));
    }
}
