using System.Security.Claims;
using Abs.Api.Auth;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/MeController.js (getCurrentUser, line 27).</summary>
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
            return StatusCode(StatusCodes.Status401Unauthorized);

        var isOldToken = User.HasClaim(JwtAuthenticationHandler.OldTokenClaim, "true");
        var user = await users.GetBrowserUserAsync(userId, isOldToken, cancellationToken);
        return user is null ? StatusCode(StatusCodes.Status404NotFound) : Ok(user);
    }
}
