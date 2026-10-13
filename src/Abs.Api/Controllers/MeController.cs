using System.Security.Claims;
using Abs.Api.Dtos;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/MeController.js (getCurrentUser).</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(UserService users) : ControllerBase
{
    // GET /api/me
    [HttpGet]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(UserDto.From(user));
    }
}
