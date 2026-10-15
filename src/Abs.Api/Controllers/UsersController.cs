using System.Security.Claims;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/UserController.js (findAll, line 29).</summary>
[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(UserService users) : ControllerBase
{
    private bool IsAdmin =>
        User.FindFirstValue(ClaimTypes.Role) is "root" or "admin";

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? include, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        // include=latestSession needs playback sessions; Node keeps serving those requests.
        if ((include ?? string.Empty).Split(',').Select(x => x.Trim()).Contains("latestSession"))
            return StatusCode(StatusCodes.Status501NotImplemented);

        var hideRootToken = User.FindFirstValue(ClaimTypes.Role) != "root";
        var all = await users.GetMinimalUsersAsync(hideRootToken, cancellationToken);
        return Ok(new { users = all });
    }

    // GET /api/users/:id (UserController.js:59). Node adds display titles and covers to every media progress
    // row; that is not implemented, so Node keeps serving this route.
    [HttpGet("{id}")]
    public IActionResult GetById(string id) =>
        IsAdmin ? StatusCode(StatusCodes.Status501NotImplemented) : StatusCode(StatusCodes.Status403Forbidden);
}
