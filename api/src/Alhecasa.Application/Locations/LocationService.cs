using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Common;

namespace Alhecasa.Application.Locations;

/// <summary>Sugerencias para los formularios: lo que ya existe en el catálogo de ubicaciones.</summary>
public interface ILocationService
{
    /// <summary>Los 32 estados de México más los que se hayan dado de alta, por orden alfabético.</summary>
    Task<IReadOnlyList<string>> ListStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Ciudades del estado indicado (o de todos si no se indica) que contienen el texto.</summary>
    Task<IReadOnlyList<string>> SearchCitiesAsync(string? state, string? text, CancellationToken cancellationToken = default);

    /// <summary>Colonias de la ciudad indicada que contienen el texto. Vacío si no se conoce el estado o la ciudad.</summary>
    Task<IReadOnlyList<string>> SearchNeighborhoodsAsync(string? state, string? city, string? text, CancellationToken cancellationToken = default);
}

public sealed class LocationService(ILocationRepository locations) : ILocationService
{
    /// <summary>Máximo de sugerencias por consulta.</summary>
    public const int MaxResults = 50;

    public async Task<IReadOnlyList<string>> ListStatesAsync(CancellationToken cancellationToken = default)
    {
        var known = await locations.ListStatesAsync(cancellationToken);
        return MexicanStates.Names
            .Concat(known.Select(s => s.Name))
            .DistinctBy(Slug.From)
            .OrderBy(Slug.From, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> SearchCitiesAsync(string? state, string? text, CancellationToken cancellationToken = default)
    {
        Guid? stateId = null;
        if (!string.IsNullOrWhiteSpace(state))
        {
            var found = await locations.FindStateAsync(StateSlug(state), cancellationToken);
            if (found is null)
            {
                return [];
            }

            stateId = found.Id;
        }

        return await locations.SearchCityNamesAsync(stateId, Clean(text), MaxResults, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchNeighborhoodsAsync(string? state, string? city, string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(city))
        {
            return [];
        }

        var geoState = await locations.FindStateAsync(StateSlug(state), cancellationToken);
        if (geoState is null)
        {
            return [];
        }

        var geoCity = await locations.FindCityAsync(geoState.Id, Slug.From(city), geoState.Slug, cancellationToken);
        return geoCity is null ? [] : await locations.SearchNeighborhoodNamesAsync(geoCity.Id, Clean(text), MaxResults, cancellationToken);
    }

    private static string StateSlug(string state) => Slug.From(MexicanStates.Canonical(state) ?? state);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
