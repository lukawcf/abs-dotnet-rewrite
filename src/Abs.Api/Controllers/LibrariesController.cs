using Abs.Api.Abstractions;
using Abs.Api.Dtos;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/LibraryController.js (findAll, findOne, middleware).</summary>
[ApiController]
[Authorize]
[Route("api/libraries")]
public class LibrariesController(LibraryService libraries, IPermissionService permissions) : ControllerBase
{
    // GET /api/libraries
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? include, CancellationToken cancellationToken)
    {
        // include=stats needs Node-only aggregate queries; Node keeps serving those requests.
        if (HasInclude(include, "stats"))
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var accessible = await permissions.GetLibrariesAccessibleAsync(cancellationToken);
        if (accessible is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await libraries.GetAllAsync(accessible, cancellationToken);
        return Ok(new LibrariesResponse(result));
    }

    // GET /api/libraries/:id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromQuery] string? include, CancellationToken cancellationToken)
    {
        if (!await permissions.CanAccessLibraryAsync(id, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        // include=filterdata uses Node's in-memory filter cache; Node keeps serving those requests.
        if (HasInclude(include, "filterdata"))
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var library = await libraries.GetByIdAsync(id, cancellationToken);
        return library is null ? NotFound("Library not found") : Ok(library);
    }

    private static bool HasInclude(string? include, string name) =>
        (include ?? string.Empty).Split(',').Contains(name);
}
