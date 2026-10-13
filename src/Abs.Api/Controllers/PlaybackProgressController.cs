using System.Security.Claims;
using System.Text.Json;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

// Node MeController.js:107-115,234-245; ApiRouter.js:176,182-183.
[ApiController]
[Authorize]
[Route("api/me/progress")]
public class PlaybackProgressController(PlaybackProgressService progress, IConfiguration configuration) : ControllerBase
{
    // Node MeController.js:275-288. A separate explicit gate preserves read-only defaults.
    [HttpPatch("batch/update")]
    public async Task<IActionResult> Batch([FromBody] JsonElement payload, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ProgressWrites:Enabled")) return StatusCode(501);
        if (payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() == 0)
            return Plain(400, "Missing request payload");
        await progress.BatchUpdateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, payload, ct);
        return Plain(200, "OK");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ProgressWrites:Enabled")) return StatusCode(501);
        var removed = await progress.DeleteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct);
        return removed ? Plain(200, "OK") : Plain(404, "Not Found");
    }

    private static ContentResult Plain(int status, string message) =>
        new() { StatusCode = status, Content = message, ContentType = "text/plain" };

    [HttpPatch("{id}/{episodeId?}")]
    public async Task<IActionResult> Patch(string id, string? episodeId, [FromBody] JsonElement patch, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ProgressWrites:Enabled")) return StatusCode(501);
        if (patch.ValueKind != JsonValueKind.Object) return BadRequest("Expected a JSON object");
        var result = await progress.UpsertAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, episodeId, patch, ct);
        return new ContentResult { StatusCode = result.Status, Content = result.Message, ContentType = "text/plain" };
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(new { mediaProgress = await progress.GetAllAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct) });

    // This GET mutates state in Node. Reserve it so it cannot match the episode read route.
    [HttpGet("{id}/remove-from-continue-listening")]
    public IActionResult UnsupportedAction() => StatusCode(501);

    [HttpGet("{id}/{episodeId?}")]
    public async Task<IActionResult> Get(string id, string? episodeId, CancellationToken ct)
    {
        var result = await progress.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, episodeId, ct);
        return result is null ? new ContentResult { StatusCode = 404, Content = "Not Found", ContentType = "text/plain" } : Ok(result);
    }
}
