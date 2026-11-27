using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class LocationRepository(AlhecasaDbContext db) : ILocationRepository
{
    public async Task<IReadOnlyList<GeoState>> ListStatesAsync(CancellationToken cancellationToken = default) =>
        await db.GeoStates.AsNoTracking().OrderBy(s => s.Slug).ToListAsync(cancellationToken);

    public Task<GeoState?> FindStateAsync(string slug, CancellationToken cancellationToken = default) =>
        db.GeoStates.FirstOrDefaultAsync(s => s.Slug == slug, cancellationToken);

    public Task<GeoCity?> FindCityAsync(Guid stateId, string slug, string stateSlug, CancellationToken cancellationToken = default)
    {
        var withState = $"{slug}-{stateSlug}";
        return db.GeoCities.FirstOrDefaultAsync(
            c => c.StateId == stateId && (c.Slug == slug || c.Slug == withState),
            cancellationToken);
    }

    public Task<bool> CitySlugExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        db.GeoCities.AnyAsync(c => c.Slug == slug, cancellationToken);

    public Task<GeoCity?> GetCityBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        db.GeoCities.Include(c => c.State).AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);

    public Task<GeoNeighborhood?> FindNeighborhoodAsync(Guid cityId, string slug, CancellationToken cancellationToken = default) =>
        db.GeoNeighborhoods.FirstOrDefaultAsync(n => n.CityId == cityId && n.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<string>> SearchCityNamesAsync(Guid? stateId, string? text, int limit, CancellationToken cancellationToken = default)
    {
        var source = db.GeoCities.AsNoTracking().AsQueryable();
        if (stateId is not null)
        {
            source = source.Where(c => c.StateId == stateId);
        }

        if (text is not null)
        {
            var pattern = $"%{EscapeLike(text)}%";
            source = source.Where(c => EF.Functions.Like(c.Name, pattern, "\\"));
        }

        return await source.OrderBy(c => c.Name).Select(c => c.Name).Distinct().Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchNeighborhoodNamesAsync(Guid cityId, string? text, int limit, CancellationToken cancellationToken = default)
    {
        var source = db.GeoNeighborhoods.AsNoTracking().Where(n => n.CityId == cityId);
        if (text is not null)
        {
            var pattern = $"%{EscapeLike(text)}%";
            source = source.Where(n => EF.Functions.Like(n.Name, pattern, "\\"));
        }

        return await source.OrderBy(n => n.Name).Select(n => n.Name).Take(limit).ToListAsync(cancellationToken);
    }

    public async Task AddStateAsync(GeoState state, CancellationToken cancellationToken = default) =>
        await db.GeoStates.AddAsync(state, cancellationToken);

    public async Task AddCityAsync(GeoCity city, CancellationToken cancellationToken = default) =>
        await db.GeoCities.AddAsync(city, cancellationToken);

    public async Task AddNeighborhoodAsync(GeoNeighborhood neighborhood, CancellationToken cancellationToken = default) =>
        await db.GeoNeighborhoods.AddAsync(neighborhood, cancellationToken);

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}
