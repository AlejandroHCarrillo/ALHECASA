using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Locations;

/// <summary>Cuántas propiedades publicadas hay en una zona (una colonia, o una ciudad sin colonia) para una operación.</summary>
public sealed record ZoneCount(
    string StateName,
    string CityName,
    string CitySlug,
    string? NeighborhoodName,
    string? NeighborhoodSlug,
    OperationType Operation,
    int Count);

/// <summary>Un enlace a una zona de la que se puede ver el catálogo: una ciudad, o una colonia de una ciudad.</summary>
public sealed record ZoneLinkDto(
    string Name,
    string CityName,
    string CitySlug,
    string? NeighborhoodSlug,
    int SaleCount,
    int RentCount);

/// <summary>Lo necesario para armar la página pública de una zona: su nombre y cuántas propiedades hay en venta y en renta.</summary>
public sealed record ZoneDto(
    string CityName,
    string CitySlug,
    string StateName,
    string? NeighborhoodName,
    string? NeighborhoodSlug,
    int SaleCount,
    int RentCount,
    IReadOnlyList<ZoneLinkDto> Neighborhoods);

public interface IZoneService
{
    /// <summary>Las zonas con más propiedades publicadas (ciudades y colonias), para enlazarlas desde la portada.</summary>
    Task<IReadOnlyList<ZoneLinkDto>> GetTopAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Una ciudad, o una colonia de ella. <see cref="NotFoundException"/> si no existe o no tiene ninguna propiedad publicada
    /// (ni en venta ni en renta): así una dirección inventada, o una zona vacía, no responde como una página válida.
    /// </summary>
    Task<ZoneDto> GetAsync(string citySlug, string? neighborhoodSlug, CancellationToken cancellationToken = default);
}

public sealed class ZoneService(IPropertyRepository properties) : IZoneService
{
    public const int MaxTop = 100;

    public async Task<IReadOnlyList<ZoneLinkDto>> GetTopAsync(int limit, CancellationToken cancellationToken = default)
    {
        var rows = await properties.GetPublishedZoneCountsAsync(cancellationToken);
        var take = Math.Clamp(limit, 1, MaxTop);

        var cities = rows
            .GroupBy(r => r.CitySlug)
            .Select(g => Link(g.First().CityName, g.First().CityName, g.Key, null, g));
        var neighborhoods = rows
            .Where(r => r.NeighborhoodSlug is not null)
            .GroupBy(r => (r.CitySlug, r.NeighborhoodSlug))
            .Select(g => Link(g.First().NeighborhoodName!, g.First().CityName, g.Key.CitySlug, g.Key.NeighborhoodSlug, g));

        return cities.Concat(neighborhoods)
            .OrderByDescending(z => z.SaleCount + z.RentCount)
            .ThenBy(z => z.CitySlug, StringComparer.Ordinal)
            .ThenBy(z => z.NeighborhoodSlug, StringComparer.Ordinal)
            .Take(take)
            .ToList();
    }

    public async Task<ZoneDto> GetAsync(string citySlug, string? neighborhoodSlug, CancellationToken cancellationToken = default)
    {
        var city = Slug.From(citySlug);
        var hood = string.IsNullOrWhiteSpace(neighborhoodSlug) ? null : Slug.From(neighborhoodSlug);

        var rows = (await properties.GetPublishedZoneCountsAsync(cancellationToken))
            .Where(r => r.CitySlug == city)
            .ToList();
        if (rows.Count == 0)
        {
            throw new NotFoundException("Zone not found.");
        }

        var first = rows[0];
        var scope = hood is null ? rows : rows.Where(r => r.NeighborhoodSlug == hood).ToList();
        if (scope.Count == 0)
        {
            throw new NotFoundException("Zone not found.");
        }

        var neighborhoods = rows
            .Where(r => r.NeighborhoodSlug is not null)
            .GroupBy(r => r.NeighborhoodSlug!)
            .Select(g => Link(g.First().NeighborhoodName!, first.CityName, city, g.Key, g))
            .OrderByDescending(z => z.SaleCount + z.RentCount)
            .ThenBy(z => z.NeighborhoodSlug, StringComparer.Ordinal)
            .ToList();

        return new ZoneDto(
            first.CityName,
            city,
            first.StateName,
            hood is null ? null : scope[0].NeighborhoodName,
            hood,
            Total(scope, OperationType.Sale),
            Total(scope, OperationType.Rent),
            neighborhoods);
    }

    private static ZoneLinkDto Link(string name, string cityName, string citySlug, string? neighborhoodSlug, IEnumerable<ZoneCount> rows)
    {
        var list = rows.ToList();
        return new ZoneLinkDto(name, cityName, citySlug, neighborhoodSlug, Total(list, OperationType.Sale), Total(list, OperationType.Rent));
    }

    private static int Total(IEnumerable<ZoneCount> rows, OperationType operation) =>
        rows.Where(r => r.Operation == operation).Sum(r => r.Count);
}
