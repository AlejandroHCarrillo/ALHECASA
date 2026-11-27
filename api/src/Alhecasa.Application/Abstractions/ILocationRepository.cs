using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

/// <summary>Catálogo de estados, ciudades y colonias. Es de la plataforma: no está aislado por inmobiliaria.</summary>
public interface ILocationRepository
{
    Task<IReadOnlyList<GeoState>> ListStatesAsync(CancellationToken cancellationToken = default);

    Task<GeoState?> FindStateAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>La ciudad de un estado cuyo identificador es <paramref name="slug"/> o, si ya existía en otro estado, <paramref name="slug"/>-<paramref name="stateSlug"/>.</summary>
    Task<GeoCity?> FindCityAsync(Guid stateId, string slug, string stateSlug, CancellationToken cancellationToken = default);

    /// <summary>Si ya hay una ciudad con ese identificador (de cualquier estado).</summary>
    Task<bool> CitySlugExistsAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Una ciudad por su identificador público, con su estado.</summary>
    Task<GeoCity?> GetCityBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<GeoNeighborhood?> FindNeighborhoodAsync(Guid cityId, string slug, CancellationToken cancellationToken = default);

    /// <summary>Nombres de las ciudades que contienen el texto (todas si está vacío), de un estado si se indica, por orden alfabético.</summary>
    Task<IReadOnlyList<string>> SearchCityNamesAsync(Guid? stateId, string? text, int limit, CancellationToken cancellationToken = default);

    /// <summary>Nombres de las colonias de una ciudad que contienen el texto (todas si está vacío), por orden alfabético.</summary>
    Task<IReadOnlyList<string>> SearchNeighborhoodNamesAsync(Guid cityId, string? text, int limit, CancellationToken cancellationToken = default);

    Task AddStateAsync(GeoState state, CancellationToken cancellationToken = default);

    Task AddCityAsync(GeoCity city, CancellationToken cancellationToken = default);

    Task AddNeighborhoodAsync(GeoNeighborhood neighborhood, CancellationToken cancellationToken = default);
}
