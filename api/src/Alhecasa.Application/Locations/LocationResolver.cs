using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Locations;

/// <summary>El estado, la ciudad y la colonia del catálogo que corresponden a lo que escribió una persona.</summary>
public sealed record ResolvedLocation(
    Guid StateId,
    string StateName,
    Guid CityId,
    string CityName,
    Guid? NeighborhoodId,
    string? NeighborhoodName);

public interface ILocationResolver
{
    /// <summary>
    /// Busca en el catálogo lo que escribió la persona y, si no existe, lo da de alta (los cambios los confirma quien llama, con su unidad de trabajo).
    /// Dos escrituras de un mismo nombre («cdmx» y «Ciudad de México», «Polanco» y «polanco») dan la misma ubicación.
    /// </summary>
    Task<ResolvedLocation> ResolveAsync(string state, string city, string? neighborhood, CancellationToken cancellationToken = default);
}

/// <summary>
/// Una instancia por solicitud. Recuerda lo que ya dio de alta aunque todavía no se haya guardado: así una carga de muchas
/// propiedades de la misma ciudad no crea la ciudad dos veces.
/// </summary>
public sealed class LocationResolver(ILocationRepository locations) : ILocationResolver
{
    private readonly Dictionary<string, GeoState> _states = [];
    private readonly Dictionary<(Guid StateId, string Slug), GeoCity> _cities = [];
    private readonly Dictionary<(Guid CityId, string Slug), GeoNeighborhood> _neighborhoods = [];
    private readonly HashSet<string> _newCitySlugs = [];

    public async Task<ResolvedLocation> ResolveAsync(string state, string city, string? neighborhood, CancellationToken cancellationToken = default)
    {
        var geoState = await ResolveStateAsync(state, cancellationToken);
        var geoCity = await ResolveCityAsync(geoState, city, cancellationToken);
        var geoNeighborhood = string.IsNullOrWhiteSpace(neighborhood)
            ? null
            : await ResolveNeighborhoodAsync(geoCity, neighborhood, cancellationToken);

        return new ResolvedLocation(geoState.Id, geoState.Name, geoCity.Id, geoCity.Name, geoNeighborhood?.Id, geoNeighborhood?.Name);
    }

    private async Task<GeoState> ResolveStateAsync(string state, CancellationToken cancellationToken)
    {
        // Los 32 estados de México se reconocen por su abreviatura o nombre alterno; otro texto se acepta tal cual.
        var name = MexicanStates.Canonical(state) ?? LocationNames.Tidy(state);
        var slug = Slug.From(name);
        if (slug.Length == 0)
        {
            throw new DomainException("The state name must contain letters or numbers.");
        }

        if (_states.TryGetValue(slug, out var cached))
        {
            return cached;
        }

        var found = await locations.FindStateAsync(slug, cancellationToken);
        if (found is null)
        {
            found = new GeoState(name);
            await locations.AddStateAsync(found, cancellationToken);
        }

        _states[slug] = found;
        return found;
    }

    private async Task<GeoCity> ResolveCityAsync(GeoState state, string city, CancellationToken cancellationToken)
    {
        var name = LocationNames.Tidy(city);
        var baseSlug = Slug.From(name);
        if (baseSlug.Length == 0)
        {
            throw new DomainException("The city name must contain letters or numbers.");
        }

        if (_cities.TryGetValue((state.Id, baseSlug), out var cached))
        {
            return cached;
        }

        var found = await locations.FindCityAsync(state.Id, baseSlug, state.Slug, cancellationToken);
        if (found is null)
        {
            // El identificador de una ciudad es único en toda la plataforma: si otro estado ya tiene una con ese nombre
            // («Santa Cruz»), la nueva lleva el estado en el suyo.
            var slug = _newCitySlugs.Contains(baseSlug) || await locations.CitySlugExistsAsync(baseSlug, cancellationToken)
                ? $"{baseSlug}-{state.Slug}"
                : baseSlug;
            found = new GeoCity(state.Id, name, slug);
            _newCitySlugs.Add(slug);
            await locations.AddCityAsync(found, cancellationToken);
        }

        _cities[(state.Id, baseSlug)] = found;
        return found;
    }

    private async Task<GeoNeighborhood> ResolveNeighborhoodAsync(GeoCity city, string neighborhood, CancellationToken cancellationToken)
    {
        var name = LocationNames.Tidy(neighborhood);
        var slug = Slug.From(name);
        if (slug.Length == 0)
        {
            throw new DomainException("The neighborhood name must contain letters or numbers.");
        }

        if (_neighborhoods.TryGetValue((city.Id, slug), out var cached))
        {
            return cached;
        }

        var found = await locations.FindNeighborhoodAsync(city.Id, slug, cancellationToken);
        if (found is null)
        {
            found = new GeoNeighborhood(city.Id, name);
            await locations.AddNeighborhoodAsync(found, cancellationToken);
        }

        _neighborhoods[(city.Id, slug)] = found;
        return found;
    }
}
