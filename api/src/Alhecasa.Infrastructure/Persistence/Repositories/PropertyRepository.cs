using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class PropertyRepository(AlhecasaDbContext db) : IPropertyRepository
{
    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Properties
            .Include(p => p.Agency)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PropertyImage?> GetImageForServingAsync(Guid imageId, CancellationToken cancellationToken = default) =>
        db.PropertyImages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Id == imageId
                && !i.IsDeleted
                && db.Properties.IgnoreQueryFilters().Any(p => p.Id == i.PropertyId && !p.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<Property>> GetPagedAsync(PropertyListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<Property> source = db.Properties.AsNoTracking().Include(p => p.Agency).Include(p => p.Images);

        if (query.Search is not null)
        {
            var pattern = $"%{EscapeLike(query.Search)}%";
            source = source.Where(p =>
                EF.Functions.Like(p.Title, pattern, "\\")
                || EF.Functions.Like(p.City, pattern, "\\")
                || (p.Neighborhood != null && EF.Functions.Like(p.Neighborhood, pattern, "\\"))
                || (p.Street != null && EF.Functions.Like(p.Street, pattern, "\\")));
        }

        var total = await source.CountAsync(cancellationToken);

        // Lo más reciente primero; el Id (Guid v7, ordenable por tiempo) desempata.
        var items = await source
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Property>(items, query.Page, query.PageSize, total);
    }

    // IgnoreQueryFilters: sin sesión los filtros globales no devuelven nada. La visibilidad se define aquí, explícita
    // y en un solo lugar (la usan el listado y el detalle): no borrada, publicada y de una inmobiliaria activa y no borrada.
    private IQueryable<Property> VisiblePublicly() => db.Properties
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Include(p => p.Agency)
        .Include(p => p.Images)
        .Where(p => !p.IsDeleted
            && p.Status == PropertyStatus.Published
            && !p.Agency!.IsDeleted
            && p.Agency.IsActive);

    public Task<Property?> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        VisiblePublicly().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PagedResult<Property>> GetPublishedPagedAsync(PublicPropertyQuery query, CancellationToken cancellationToken = default)
    {
        var source = VisiblePublicly();

        if (query.Ids is { Count: > 0 })
        {
            // La visibilidad (publicada, no borrada…) ya está aplicada: un id de un borrador simplemente no aparece.
            var ids = query.Ids.ToArray();
            source = source.Where(p => ids.Contains(p.Id));
        }

        if (query.Operation is not null)
        {
            source = source.Where(p => p.Operation == query.Operation);
        }

        if (query.Type is not null)
        {
            source = source.Where(p => p.Type == query.Type);
        }

        if (query.Location is not null)
        {
            // Solo colonia, ciudad y estado: la calle no es un dato público.
            var pattern = $"%{EscapeLike(query.Location)}%";
            source = source.Where(p =>
                EF.Functions.Like(p.City, pattern, "\\")
                || EF.Functions.Like(p.State, pattern, "\\")
                || (p.Neighborhood != null && EF.Functions.Like(p.Neighborhood, pattern, "\\")));
        }

        var total = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Property>(items, query.Page, query.PageSize, total);
    }

    public async Task AddAsync(Property entity, CancellationToken cancellationToken = default) =>
        await db.Properties.AddAsync(entity, cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
