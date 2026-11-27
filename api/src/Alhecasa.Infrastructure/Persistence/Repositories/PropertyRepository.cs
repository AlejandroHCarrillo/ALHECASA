using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Application.Locations;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class PropertyRepository(AlhecasaDbContext db, TimeProvider timeProvider) : IPropertyRepository
{
    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Properties
            .Include(p => p.Agency)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<int> CountByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default) =>
        db.Properties.IgnoreQueryFilters().CountAsync(p => p.AgencyId == agencyId && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Property>> ListFeaturedAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await db.Properties.IgnoreQueryFilters()
            .Where(p => p.AgencyId == agencyId && !p.IsDeleted && p.FeaturedUntil > now)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, PropertyUsage>> GetUsageByAgencyAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var rows = await db.Properties.IgnoreQueryFilters().AsNoTracking()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.AgencyId)
            .Select(g => new { AgencyId = g.Key, Total = g.Count(), Featured = g.Count(p => p.FeaturedUntil > now) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.AgencyId, r => new PropertyUsage(r.Total, r.Featured));
    }

    public async Task<PropertyStats> GetStatsAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // Los filtros globales ya aíslan por inmobiliaria y descartan lo eliminado; el filtro explícito es una segunda barrera.
        var byStatus = await db.Properties.AsNoTracking()
            .Where(p => p.AgencyId == agencyId)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var featured = await db.Properties.AsNoTracking()
            .CountAsync(p => p.AgencyId == agencyId && p.FeaturedUntil > now, cancellationToken);

        int Of(PropertyStatus status) => byStatus.Where(r => r.Status == status).Sum(r => r.Count);

        return new PropertyStats(
            byStatus.Sum(r => r.Count),
            Of(PropertyStatus.Published),
            Of(PropertyStatus.Draft),
            Of(PropertyStatus.Paused),
            Of(PropertyStatus.Closed),
            featured);
    }

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
    private IQueryable<Property> VisiblePublicly() => PublishedRows()
        .Include(p => p.Agency)
        .Include(p => p.Images)
        .Include(p => p.CityLocation)
        .Include(p => p.NeighborhoodLocation);

    private IQueryable<Property> PublishedRows() => db.Properties
        .IgnoreQueryFilters()
        .AsNoTracking()
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

        if (query.AgencyId is not null)
        {
            // El catálogo público nunca agrupa por particular: es anónimo (ver PublicPropertyDto.AgencyId).
            source = source.Where(p => p.AgencyId == query.AgencyId && p.Agency!.Kind != AgencyKind.Individual);
        }

        if (query.Ids is { Count: > 0 })
        {
            // La visibilidad (publicada, no borrada…) ya está aplicada: un id de un borrador simplemente no aparece.
            var ids = query.Ids.ToArray();
            source = source.Where(p => ids.Contains(p.Id));
        }

        source = ApplyCriteria(source, query);

        if (query.CitySlug is not null)
        {
            // Por el catálogo de ubicaciones (no por texto): «cdmx» y «Ciudad de México» son la misma zona.
            var citySlug = query.CitySlug;
            var neighborhoodSlug = query.NeighborhoodSlug;
            source = neighborhoodSlug is null
                ? source.Where(p => db.GeoCities.Any(c => c.Id == p.CityId && c.Slug == citySlug))
                : source.Where(p => db.GeoNeighborhoods.Any(n =>
                    n.Id == p.NeighborhoodId && n.Slug == neighborhoodSlug && n.City!.Slug == citySlug));
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

        // En el catálogo público, con el orden por omisión (más recientes), los destacados van primero. Con otro orden (precio,
        // superficie) no se anteponen: el visitante pidió un orden y debe respetarse; la etiqueta «Destacado» sí se ve.
        var now = timeProvider.GetUtcNow();
        IQueryable<Property> ordered = query.Sort == PropertySort.Recent
            ? source.OrderByDescending(p => p.FeaturedUntil > now).ThenByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            : Sort(source, query.Sort);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Property>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<ZoneCount>> GetPublishedZoneCountsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
            from p in PublishedRows()
            where p.CityId != null
            join c in db.GeoCities on p.CityId equals c.Id
            join s in db.GeoStates on c.StateId equals s.Id
            join n in db.GeoNeighborhoods on p.NeighborhoodId equals n.Id into neighborhoods
            from n in neighborhoods.DefaultIfEmpty()
            group p by new
            {
                StateName = s.Name,
                CityName = c.Name,
                CitySlug = c.Slug,
                NeighborhoodName = n.Name,
                NeighborhoodSlug = n.Slug,
                p.Operation
            }
            into g
            select new
            {
                g.Key.StateName,
                g.Key.CityName,
                g.Key.CitySlug,
                g.Key.NeighborhoodName,
                g.Key.NeighborhoodSlug,
                g.Key.Operation,
                Count = g.Count()
            }).ToListAsync(cancellationToken);

        return rows
            .Select(r => new ZoneCount(r.StateName, r.CityName, r.CitySlug, r.NeighborhoodName, r.NeighborhoodSlug, r.Operation, r.Count))
            .ToList();
    }

    public async Task<IReadOnlyList<SitemapProperty>> GetPublishedForSitemapAsync(int limit, CancellationToken cancellationToken = default)
    {
        var rows = await PublishedRows()
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(limit)
            .Select(p => new { p.Id, p.CreatedAt, p.UpdatedAt, p.AgencyId, IsIndividual = p.Agency!.Kind == AgencyKind.Individual })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SitemapProperty(r.Id, r.UpdatedAt ?? r.CreatedAt, r.AgencyId, r.IsIndividual)).ToList();
    }

    public async Task<IReadOnlyList<Property>> ListWithoutLocationAsync(CancellationToken cancellationToken = default) =>
        await db.Properties.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.CityId == null)
            .ToListAsync(cancellationToken);

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
