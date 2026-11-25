using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Properties;

/// <summary>
/// Lo que ve un visitante sin sesión. No incluye calle ni código postal (la dirección exacta se da al contactar),
/// ni el estado, ni datos internos de la inmobiliaria. La ubicación en el mapa es aproximada (ver <c>ApproximateCoordinate</c>).
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
    decimal? ApproximateLatitude,
    decimal? ApproximateLongitude,
    IReadOnlyList<Guid> ImageIds,
    string? AgencyName,
    int? ParkingSpaces,
    bool PetsAllowed,
    bool Furnished,
    IReadOnlyList<Amenity> Amenities);

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
    IReadOnlyList<Guid>? Ids = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    CurrencyCode? Currency = null,
    int? MinBedrooms = null,
    int? MinBathrooms = null,
    int? MinParking = null,
    decimal? MinArea = null,
    decimal? MaxArea = null,
    bool? PetsAllowed = null,
    bool? Furnished = null,
    IReadOnlyList<Amenity>? Amenities = null,
    PropertySort Sort = PropertySort.Recent) : IPropertyCriteria
{
    public const int MaxPageSize = PropertyListQuery.MaxPageSize;

    /// <summary>Máximo de identificadores por solicitud: van en la dirección, que tiene un tamaño límite.</summary>
    public const int MaxIds = 100;

    /// <summary>Devuelve una copia con la ubicación limpia, los identificadores sin repetir y la paginación acotada a valores seguros.</summary>
    /// <remarks>Un criterio inválido (rango invertido, precio sin moneda…) lanza <see cref="DomainException"/>, que responde 400.</remarks>
    public PublicPropertyQuery Normalize()
    {
        PropertyCriteriaRules.Validate(this);
        return this with
        {
            Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim(),
            Page = Math.Max(1, Page),
            PageSize = Math.Clamp(PageSize, 1, MaxPageSize),
            Ids = Ids is { Count: > 0 } ? Ids.Distinct().ToList() : null,
            Amenities = PropertyCriteriaRules.NormalizeAmenities(Amenities)
        };
    }
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

        var page = await properties.GetPublishedPagedAsync(normalized, cancellationToken: cancellationToken);
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

    /// <summary>Cómo se muestra quien publica. Un particular es anónimo: nunca se publica su nombre real.</summary>
    public const string IndividualPublisherName = "Particular";

    private static string? PublisherName(Agency? agency) =>
        agency is { IsIndividual: true } ? IndividualPublisherName : agency?.Name;

    internal static PublicPropertyDto ToDto(Property property) => new(
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
        ApproximateCoordinate(property.Latitude),
        ApproximateCoordinate(property.Longitude),
        OrderedImageIds(property),
        PublisherName(property.Agency),
        property.ParkingSpaces,
        property.PetsAllowed,
        property.Furnished,
        AmenityFlags.ToList(property.Amenities));

    /// <summary>
    /// Tres decimales son unos 110 m: se ve la zona en el mapa, no la puerta de la casa. La ubicación exacta, como la calle,
    /// se da al contactar. La portada dibuja un círculo, no un marcador.
    /// </summary>
    internal static decimal? ApproximateCoordinate(decimal? value) => value is null ? null : Math.Round(value.Value, 3);

    internal static IReadOnlyList<Guid> OrderedImageIds(Property property) =>
        property.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Id).ToList();
}
