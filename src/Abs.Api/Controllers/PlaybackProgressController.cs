using System.Security.Claims;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

// Node MeController.js:107-115,234-245; ApiRouter.js:176,182-183.
[ApiController]
[Authorize]
[Route("api/me/progress")]
public class PlaybackProgressController(PlaybackProgressService progress) : ControllerBase
{
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
