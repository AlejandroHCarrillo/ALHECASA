using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Properties;

/// <summary>
/// Lo que ve un visitante sin sesión. No incluye calle ni código postal (la dirección exacta se da al contactar),
/// ni el estado, ni datos internos de la inmobiliaria.
/// </summary>
public sealed record PublicPropertyDto(
    Guid Id,
    string Title,
    string? Description,
    PropertyType Type,
    OperationType Operation,
    decimal Price,
    CurrencyCode Currency,
    string? Neighborhood,
    string City,
    string State,
    int? Bedrooms,
    int? Bathrooms,
    decimal? ConstructionArea,
    decimal? LandArea,
    IReadOnlyList<Guid> ImageIds,
    string? AgencyName);

/// <summary>Contenido de una imagen listo para enviarse al visitante.</summary>
public sealed record ImageContent(Stream Content, string ContentType);

/// <summary>Filtros del catálogo público. Todos son opcionales y se combinan con «y».</summary>
/// <param name="Location">Texto que debe aparecer en la colonia, la ciudad o el estado.</param>
/// <param name="Ids">
/// Si se envía, solo se devuelven estas propiedades (siempre que además estén publicadas). Lo usa la portada para
/// «Mis favoritas», que el visitante guarda en su navegador. Vacío equivale a no filtrar por identificador.
/// </param>
public sealed record PublicPropertyQuery(
    string? Location = null,
    PropertyType? Type = null,
    OperationType? Operation = null,
    int Page = 1,
    int PageSize = 10,
    IReadOnlyList<Guid>? Ids = null)
{
    public const int MaxPageSize = PropertyListQuery.MaxPageSize;

    /// <summary>Máximo de identificadores por solicitud: van en la dirección, que tiene un tamaño límite.</summary>
    public const int MaxIds = 100;

    /// <summary>Devuelve una copia con la ubicación limpia, los identificadores sin repetir y la paginación acotada a valores seguros.</summary>
    public PublicPropertyQuery Normalize() => this with
    {
        Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim(),
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize),
        Ids = Ids is { Count: > 0 } ? Ids.Distinct().ToList() : null
    };
}

public interface IPublicPropertyService
{
    Task<PagedResult<PublicPropertyDto>> GetPublishedAsync(PublicPropertyQuery query, CancellationToken cancellationToken = default);

    /// <summary>Detalle de una propiedad publicada. <see cref="NotFoundException"/> si no existe o no es visible (borrador, pausada, cerrada o eliminada).</summary>
    Task<PublicPropertyDto> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Abre una imagen por su identificador, o devuelve null si no existe.</summary>
    Task<ImageContent?> OpenImageAsync(Guid imageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Catálogo público: solo propiedades publicadas, de inmobiliarias activas, de todas las inmobiliarias.
/// No depende del usuario actual (no hay sesión), por eso el repositorio ignora el aislamiento por inmobiliaria
/// y aplica por su cuenta estas reglas de visibilidad.
/// </summary>
public sealed class PublicPropertyService(IPropertyRepository properties, IImageStorage storage) : IPublicPropertyService
{
    public async Task<PagedResult<PublicPropertyDto>> GetPublishedAsync(PublicPropertyQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query.Normalize();
        if (normalized.Ids?.Count > PublicPropertyQuery.MaxIds)
        {
            throw new DomainException($"At most {PublicPropertyQuery.MaxIds} ids can be requested at once.");
        }

        var page = await properties.GetPublishedPagedAsync(normalized, cancellationToken);
        return page.Map(ToDto);
    }

    public async Task<PublicPropertyDto> GetPublishedByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ToDto(await properties.GetPublishedByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Property not found."));

    /// <summary>
    /// Las imágenes se sirven por su identificador (un Guid no adivinable) también cuando la propiedad aún es borrador,
    /// para que el personal las vea sin enviar el token en cada etiqueta de imagen. Una propiedad eliminada deja de servirlas.
    /// </summary>
    public async Task<ImageContent?> OpenImageAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await properties.GetImageForServingAsync(imageId, cancellationToken);
        if (image is null)
        {
            return null;
        }

        var content = await storage.OpenReadAsync(image.StorageKey, cancellationToken);
        return content is null ? null : new ImageContent(content, image.ContentType);
    }

    private static PublicPropertyDto ToDto(Property property) => new(
        property.Id,
        property.Title,
        property.Description,
        property.Type,
        property.Operation,
        property.Price,
        property.Currency,
        property.Neighborhood,
        property.City,
        property.State,
        property.Bedrooms,
        property.Bathrooms,
        property.ConstructionArea,
        property.LandArea,
        OrderedImageIds(property),
        property.Agency?.Name);

    internal static IReadOnlyList<Guid> OrderedImageIds(Property property) =>
        property.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Id).ToList();
}
