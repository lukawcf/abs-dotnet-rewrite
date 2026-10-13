using System.Security.Claims;
using Abs.Api.Dtos;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/UserController.js (findAll, findOne).</summary>
[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(UserService users) : ControllerBase
{
    private bool IsAdmin =>
        User.FindFirstValue(ClaimTypes.Role) is "root" or "admin";

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        var all = await users.GetAllAsync(cancellationToken);
        return Ok(new { users = all.Select(UserDto.From).ToList() });
    }

    // GET /api/users/:id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden);

        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(UserDto.From(user));
    }
}
