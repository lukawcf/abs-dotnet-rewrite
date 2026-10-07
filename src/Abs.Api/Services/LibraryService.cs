using Abs.Api.Dtos;
using Abs.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.Api.Services;

public class LibraryService(AbsDbContext db)
{
    /// <summary>All libraries ordered by displayOrder, optionally limited to the given ids (empty = all).</summary>
    public async Task<IReadOnlyList<LibraryDto>> GetAllAsync(
        IReadOnlyCollection<string> onlyLibraryIds, CancellationToken cancellationToken = default)
    {
        var query = db.Libraries.Include(x => x.Folders).AsQueryable();
        if (onlyLibraryIds.Count > 0)
        {
            query = query.Where(x => onlyLibraryIds.Contains(x.Id));
        }

        var libraries = await query.OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
        return libraries.Select(LibraryDto.From).ToList();
    }

    public async Task<LibraryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var library = await db.Libraries.Include(x => x.Folders)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return library is null ? null : LibraryDto.From(library);
    }
}
