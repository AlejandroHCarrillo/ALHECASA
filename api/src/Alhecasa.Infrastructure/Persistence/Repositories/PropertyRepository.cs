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

    public Task<int> CountByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default) =>
        db.Properties.IgnoreQueryFilters().CountAsync(p => p.AgencyId == agencyId && !p.IsDeleted, cancellationToken);

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

        if (query.Status is not null)
        {
            source = source.Where(p => p.Status == query.Status);
        }

        source = ApplyCriteria(source, query);

        var total = await source.CountAsync(cancellationToken);

        var items = await Sort(source, query.Sort)
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

    public async Task<PagedResult<Property>> GetPublishedPagedAsync(PublicPropertyQuery query, Guid? agencyId = null, CancellationToken cancellationToken = default)
    {
        var source = VisiblePublicly();

        if (agencyId is not null)
        {
            source = source.Where(p => p.AgencyId == agencyId);
        }

        if (query.Ids is { Count: > 0 })
        {
            // La visibilidad (publicada, no borrada…) ya está aplicada: un id de un borrador simplemente no aparece.
            var ids = query.Ids.ToArray();
            source = source.Where(p => ids.Contains(p.Id));
        }

        source = ApplyCriteria(source, query);

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

        var items = await Sort(source, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Property>(items, query.Page, query.PageSize, total);
    }

    public async Task AddAsync(Property entity, CancellationToken cancellationToken = default) =>
        await db.Properties.AddAsync(entity, cancellationToken);

    /// <summary>
    /// Criterios comunes del catálogo público y de la lista del personal. Cada uno solo actúa si se indicó, y todos se combinan con «y».
    /// Un dato que la propiedad no tiene (por ejemplo, recámaras de un terreno) no cumple un mínimo.
    /// </summary>
    private static IQueryable<Property> ApplyCriteria(IQueryable<Property> source, IPropertyCriteria c)
    {
        if (c.Operation is not null)
        {
            source = source.Where(p => p.Operation == c.Operation);
        }

        if (c.Type is not null)
        {
            source = source.Where(p => p.Type == c.Type);
        }

        // El precio solo se compara dentro de una moneda: no hay conversión. La validación exige moneda cuando hay un límite.
        if (c.Currency is not null && (c.MinPrice is not null || c.MaxPrice is not null))
        {
            source = source.Where(p => p.Currency == c.Currency);
        }

        if (c.MinPrice is not null)
        {
            source = source.Where(p => p.Price >= c.MinPrice);
        }

        if (c.MaxPrice is not null)
        {
            source = source.Where(p => p.Price <= c.MaxPrice);
        }

        if (c.MinBedrooms is not null)
        {
            source = source.Where(p => p.Bedrooms >= c.MinBedrooms);
        }

        if (c.MinBathrooms is not null)
        {
            source = source.Where(p => p.Bathrooms >= c.MinBathrooms);
        }

        if (c.MinParking is not null)
        {
            source = source.Where(p => p.ParkingSpaces >= c.MinParking);
        }

        // Superficie: la construida y, si no tiene (un terreno), la del terreno.
        if (c.MinArea is not null)
        {
            source = source.Where(p => (p.ConstructionArea ?? p.LandArea) >= c.MinArea);
        }

        if (c.MaxArea is not null)
        {
            source = source.Where(p => (p.ConstructionArea ?? p.LandArea) <= c.MaxArea);
        }

        if (c.PetsAllowed == true)
        {
            source = source.Where(p => p.PetsAllowed);
        }

        if (c.Furnished == true)
        {
            source = source.Where(p => p.Furnished);
        }

        var mask = AmenityFlags.Combine(c.Amenities);
        if (mask != Amenity.None)
        {
            source = source.Where(p => (p.Amenities & mask) == mask);
        }

        return source;
    }

    /// <summary>Orden pedido; siempre desempata por antigüedad y por Id (Guid v7, ordenable por tiempo), así que la paginación es estable.</summary>
    private static IQueryable<Property> Sort(IQueryable<Property> source, PropertySort sort) => sort switch
    {
        PropertySort.PriceAsc => source.OrderBy(p => p.Price).ThenByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
        PropertySort.PriceDesc => source.OrderByDescending(p => p.Price).ThenByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
        PropertySort.AreaDesc => source
            .OrderByDescending(p => p.ConstructionArea ?? p.LandArea)
            .ThenByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
        _ => source.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
    };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
