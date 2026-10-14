using Abs.Api.Abstractions;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/LibraryItemController.js (findOne 60-100, middleware 1220-1250).</summary>
[ApiController]
[Authorize]
[Route("api/items")]
public class ItemsController(LibraryItemService items, IPermissionService permissions) : ControllerBase
{
    // GET /api/items/:id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, [FromQuery] string? expanded, CancellationToken cancellationToken)
    {
        // expanded=1 adds tracks, progress, feeds and shares that Node builds; Node keeps serving those requests.
        if (QueryNumber.IsOne(expanded))
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var lookup = await items.GetByIdAsync(id, cancellationToken);
        if (lookup is null)
        {
            return PlainStatus.NotFound();
        }

        // Podcast items have a different shape and are not rewritten yet.
        if (lookup.Item is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var item = lookup.Item;
        var rules = await permissions.GetItemAccessRulesAsync(cancellationToken);
        var canAccess = item.LibraryId is not null
            && await permissions.CanAccessLibraryAsync(item.LibraryId, cancellationToken)
            && rules is not null
            && rules.CanAccessItem(item.Media.Metadata.Explicit == true, item.Media.Tags);
        return canAccess ? Ok(item) : PlainStatus.Forbidden();
    }
}
