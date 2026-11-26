using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Application.Locations;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public interface IPropertyRepository
{
    /// <summary>Obtiene una propiedad respetando el aislamiento por inmobiliaria y el borrado lógico.</summary>
    Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Property>> GetPagedAsync(PropertyListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Catálogo público: propiedades publicadas de inmobiliarias activas, de todas las inmobiliarias.
    /// Ignora el aislamiento por inmobiliaria (el visitante no tiene una), así que aplica por sí mismo las reglas de visibilidad.
    /// </summary>
    /// <summary>Una propiedad con las mismas reglas de visibilidad pública que el listado, o null si no es visible.</summary>
    Task<Property?> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="agencyId">Si se indica, solo las publicadas de esa inmobiliaria (la galería del personal). No es un filtro del catálogo público.</param>
    Task<PagedResult<Property>> GetPublishedPagedAsync(PublicPropertyQuery query, Guid? agencyId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca una imagen por su identificador para servirla sin sesión: ignora el aislamiento por inmobiliaria
    /// y descarta las imágenes de propiedades eliminadas.
    /// </summary>
    Task<PropertyImage?> GetImageForServingAsync(Guid imageId, CancellationToken cancellationToken = default);

    /// <summary>Propiedades no eliminadas de una inmobiliaria (borradores incluidos), sin importar quién consulta.</summary>
    Task<int> CountByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default);

    /// <summary>Propiedades de una inmobiliaria que están destacadas en este momento (con seguimiento de cambios, para poder quitarles el destacado).</summary>
    Task<IReadOnlyList<Property>> ListFeaturedAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Propiedades no eliminadas y destacadas en este momento de cada inmobiliaria que tenga alguna (para el tablero del administrador de plataforma).</summary>
    Task<IReadOnlyDictionary<Guid, PropertyUsage>> GetUsageByAgencyAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Cuántas propiedades no eliminadas tiene una inmobiliaria en cada estado, y cuántas están destacadas en este momento.</summary>
    Task<PropertyStats> GetStatsAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Cuántas propiedades publicadas (con las reglas de visibilidad del catálogo público) hay por ciudad, colonia y operación. Solo las enlazadas al catálogo de ubicaciones.</summary>
    Task<IReadOnlyList<ZoneCount>> GetPublishedZoneCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Las propiedades publicadas más recientes (hasta <paramref name="limit"/>), para el mapa del sitio.</summary>
    Task<IReadOnlyList<SitemapProperty>> GetPublishedForSitemapAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Propiedades que todavía no están enlazadas al catálogo de ubicaciones (las anteriores a él), con seguimiento de cambios y sin filtros de inmobiliaria.</summary>
    Task<IReadOnlyList<Property>> ListWithoutLocationAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Property entity, CancellationToken cancellationToken = default);
}
