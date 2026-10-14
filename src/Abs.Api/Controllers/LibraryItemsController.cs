using Abs.Api.Abstractions;
using Abs.Api.Dtos;
using Abs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Node source: server/controllers/LibraryController.js (getLibraryItems 604-639, middleware 1475-1498).</summary>
[ApiController]
[Authorize]
[Route("api/libraries/{id}/items")]
public class LibraryItemsController(LibraryService libraries, LibraryItemService items, IPermissionService permissions) : ControllerBase
{
    // GET /api/libraries/:id/items
    [HttpGet]
    public async Task<IActionResult> GetItems(
        string id,
        [FromQuery] string? limit,
        [FromQuery] string? page,
        [FromQuery] string? sort,
        [FromQuery] string? desc,
        [FromQuery] string? filter,
        [FromQuery] string? minified,
        [FromQuery] string? collapseseries,
        [FromQuery] string? include,
        CancellationToken cancellationToken)
    {
        if (!await permissions.CanAccessLibraryAsync(id, cancellationToken))
        {
            return PlainStatus.Forbidden();
        }

        var library = await libraries.GetByIdAsync(id, cancellationToken);
        if (library is null)
        {
            return NotFound("Library not found");
        }

        var limitValue = QueryNumber.ParsePaging(limit);
        var pageValue = QueryNumber.ParsePaging(page);
        if (limitValue is null)
        {
            return BadRequest("Invalid request. limit must be a positive integer");
        }

        if (pageValue is null)
        {
            return BadRequest("Invalid request. page must be a positive integer");
        }

        var includes = (include ?? string.Empty)
            .Split(',').Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToList();
        var itemSort = LibraryItemService.ParseSort(sort);
        var rules = await permissions.GetItemAccessRulesAsync(cancellationToken);

        // Everything below needs Node: filters, series collapsing, per-user progress sorts, includes,
        // podcast libraries and tag restricted users. Returning 501 lets the request go to Node.
        if (!string.IsNullOrEmpty(filter)
            || collapseseries == "1"
            || includes.Count > 0
            || itemSort is null
            || library.MediaType != "book"
            || rules is null
            || rules.RestrictsListByTags)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        var offset = pageValue.Value * limitValue.Value;
        var (results, total) = await items.GetPageAsync(
            id, itemSort.Value, desc == "1", limitValue.Value, offset, rules.CanAccessExplicit, cancellationToken);

        return Ok(new LibraryItemsResponse(
            results,
            total,
            limitValue.Value,
            pageValue.Value,
            sort,
            desc == "1",
            filter,
            library.MediaType,
            minified == "1",
            collapseseries == "1",
            string.Join(',', includes),
            offset));
    }
}
