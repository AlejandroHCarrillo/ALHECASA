using Alhecasa.Application.Common;
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

    Task<PagedResult<Property>> GetPublishedPagedAsync(PublicPropertyQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca una imagen por su identificador para servirla sin sesión: ignora el aislamiento por inmobiliaria
    /// y descarta las imágenes de propiedades eliminadas.
    /// </summary>
    Task<PropertyImage?> GetImageForServingAsync(Guid imageId, CancellationToken cancellationToken = default);

    Task AddAsync(Property entity, CancellationToken cancellationToken = default);
}
