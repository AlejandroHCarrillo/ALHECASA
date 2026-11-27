using System.Diagnostics.CodeAnalysis;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

/// <summary>Datos editables de una propiedad (todo salvo la inmobiliaria a la que pertenece).</summary>
public sealed record PropertyData(
    string Title,
    string? Description,
    PropertyType Type,
    OperationType Operation,
    PropertyStatus Status,
    decimal Price,
    CurrencyCode Currency,
    string? Street,
    string? Neighborhood,
    string City,
    string State,
    string? PostalCode,
    int? Bedrooms,
    int? Bathrooms,
    decimal? ConstructionArea,
    decimal? LandArea,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? ParkingSpaces = null,
    bool PetsAllowed = false,
    bool Furnished = false,
    Amenity Amenities = Amenity.None,
    Guid? StateId = null,
    Guid? CityId = null,
    Guid? NeighborhoodId = null);

/// <summary>
/// Propiedad del inventario de una inmobiliaria. Pertenece siempre a una (es el tenant) y nunca cambia de inmobiliaria.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Property es el término del dominio (ver estrategia.md); el conflicto con Visual Basic no aplica a esta solución solo C#.")]
public class Property : BaseEntity
{
    private Property()
    {
    }

    public Property(Guid agencyId, PropertyData data)
    {
        if (agencyId == Guid.Empty)
        {
            throw new DomainException("A property must belong to an agency.");
        }

        AgencyId = agencyId;
        Apply(data);
    }

    public Guid AgencyId { get; private set; }

    public Agency? Agency { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PropertyType Type { get; private set; }

    public OperationType Operation { get; private set; }

    public PropertyStatus Status { get; private set; }

    public decimal Price { get; private set; }

    public CurrencyCode Currency { get; private set; }

    public string? Street { get; private set; }

    public string? Neighborhood { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string? PostalCode { get; private set; }

    /// <summary>Estado del catálogo de ubicaciones al que corresponde <see cref="State"/>. Nulo en propiedades anteriores al catálogo, hasta que se enlazan.</summary>
    public Guid? StateId { get; private set; }

    /// <summary>Ciudad del catálogo a la que corresponde <see cref="City"/>. De aquí salen las páginas públicas por zona.</summary>
    public Guid? CityId { get; private set; }

    /// <summary>Colonia del catálogo a la que corresponde <see cref="Neighborhood"/>, si se capturó.</summary>
    public Guid? NeighborhoodId { get; private set; }

    /// <summary>La ciudad del catálogo (para el identificador de su página pública). Solo está cargada si se pidió con la propiedad.</summary>
    public GeoCity? CityLocation { get; private set; }

    /// <summary>La colonia del catálogo. Solo está cargada si se pidió con la propiedad.</summary>
    public GeoNeighborhood? NeighborhoodLocation { get; private set; }

    public int? Bedrooms { get; private set; }

    public int? Bathrooms { get; private set; }

    /// <summary>Superficie construida en metros cuadrados.</summary>
    public decimal? ConstructionArea { get; private set; }

    /// <summary>Superficie del terreno en metros cuadrados.</summary>
    public decimal? LandArea { get; private set; }

    /// <summary>Latitud exacta en grados (-90 a 90). Va siempre junto con <see cref="Longitude"/>. No es un dato público: la portada solo recibe una aproximación.</summary>
    public decimal? Latitude { get; private set; }

    /// <summary>Longitud exacta en grados (-180 a 180).</summary>
    public decimal? Longitude { get; private set; }

    /// <summary>Cajones de estacionamiento. Nulo si no se capturó; 0 significa que no tiene.</summary>
    public int? ParkingSpaces { get; private set; }

    public bool PetsAllowed { get; private set; }

    public bool Furnished { get; private set; }

    /// <summary>Amenidades como máscara de bits (ver <see cref="Amenity"/>).</summary>
    public Amenity Amenities { get; private set; }

    /// <summary>Hasta cuándo está destacada (paquete de destacados). Nulo, o una fecha pasada, = no destacada.</summary>
    public DateTimeOffset? FeaturedUntil { get; private set; }

    public bool IsFeaturedAt(DateTimeOffset now) => FeaturedUntil > now;

    /// <summary>Destaca la propiedad hasta la fecha indicada (el fin del paquete que se usa).</summary>
    public void Feature(DateTimeOffset until, DateTimeOffset now)
    {
        if (until <= now)
        {
            throw new DomainException("A property can only be featured until a future date.");
        }

        FeaturedUntil = until;
    }

    public void Unfeature() => FeaturedUntil = null;

    /// <summary>Máximo de imágenes por propiedad.</summary>
    public const int MaxImages = 10;

    private readonly List<PropertyImage> _images = [];

    /// <summary>Galería de la propiedad (la primera imagen es la portada). Solo está completa si se cargó con la propiedad.</summary>
    public IReadOnlyCollection<PropertyImage> Images => _images;

    public void Update(PropertyData data) => Apply(data);

    /// <summary>Fija o quita la ubicación. Latitud y longitud van juntas: las dos con valor o las dos nulas.</summary>
    public void SetCoordinates(decimal? latitude, decimal? longitude)
    {
        ValidateCoordinates(latitude, longitude);
        Latitude = Round(latitude);
        Longitude = Round(longitude);
    }

    /// <summary>Agrega una imagen al final de la galería. No puede superar <see cref="MaxImages"/>.</summary>
    public PropertyImage AddImage(string storageKey, string contentType, long sizeBytes)
    {
        if (_images.Count >= MaxImages)
        {
            throw new DomainException($"A property can have at most {MaxImages} images.");
        }

        var nextOrder = _images.Count == 0 ? 0 : _images.Max(i => i.SortOrder) + 1;
        var image = new PropertyImage(Id, AgencyId, storageKey, contentType, sizeBytes, nextOrder);
        _images.Add(image);
        return image;
    }

    /// <summary>Quita una imagen de la galería y la devuelve (para borrar también su archivo). Null si no pertenece a la propiedad.</summary>
    public PropertyImage? RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is not null)
        {
            _images.Remove(image);
        }

        return image;
    }

    private void Apply(PropertyData data)
    {
        if (string.IsNullOrWhiteSpace(data.Title))
        {
            throw new DomainException("The title is required.");
        }

        if (string.IsNullOrWhiteSpace(data.City) || string.IsNullOrWhiteSpace(data.State))
        {
            throw new DomainException("The city and the state are required.");
        }

        if (data.Price <= 0)
        {
            throw new DomainException("The price must be greater than zero.");
        }

        if (data.Bedrooms is < 0 || data.Bathrooms is < 0 || data.ConstructionArea is < 0 || data.LandArea is < 0)
        {
            throw new DomainException("Rooms and areas cannot be negative.");
        }

        if (data.ParkingSpaces is < 0)
        {
            throw new DomainException("Parking spaces cannot be negative.");
        }

        if (data.Amenities != Amenity.None && AmenityFlags.Combine(AmenityFlags.ToList(data.Amenities)) != data.Amenities)
        {
            throw new DomainException("The amenities are not valid.");
        }

        ValidateCoordinates(data.Latitude, data.Longitude);

        Title = data.Title;
        Description = data.Description;
        Type = data.Type;
        Operation = data.Operation;
        Status = data.Status;
        Price = data.Price;
        Currency = data.Currency;
        Street = data.Street;
        Neighborhood = data.Neighborhood;
        City = data.City;
        State = data.State;
        PostalCode = data.PostalCode;
        StateId = data.StateId;
        CityId = data.CityId;
        NeighborhoodId = data.NeighborhoodId;
        Bedrooms = data.Bedrooms;
        Bathrooms = data.Bathrooms;
        ConstructionArea = data.ConstructionArea;
        LandArea = data.LandArea;
        Latitude = Round(data.Latitude);
        Longitude = Round(data.Longitude);
        ParkingSpaces = data.ParkingSpaces;
        PetsAllowed = data.PetsAllowed;
        Furnished = data.Furnished;
        Amenities = data.Amenities;
    }

    /// <summary>
    /// Completa los datos nuevos de una propiedad que se creó antes de que existieran (los de ejemplo).
    /// Solo actúa si todavía no se capturó nada de ellos; nunca pisa lo que alguien ya puso.
    /// </summary>
    public bool CompleteAdvancedDetails(int? parkingSpaces, bool petsAllowed, bool furnished, Amenity amenities)
    {
        if (ParkingSpaces is not null || PetsAllowed || Furnished || Amenities != Amenity.None)
        {
            return false;
        }

        ParkingSpaces = parkingSpaces;
        PetsAllowed = petsAllowed;
        Furnished = furnished;
        Amenities = amenities;
        return true;
    }

    /// <summary>
    /// Enlaza la propiedad con el catálogo de ubicaciones y toma de él los nombres oficiales (así «cdmx» y «Ciudad de México» quedan iguales).
    /// </summary>
    public void SetLocation(Guid stateId, string stateName, Guid cityId, string cityName, Guid? neighborhoodId, string? neighborhoodName)
    {
        StateId = stateId;
        State = stateName;
        CityId = cityId;
        City = cityName;
        NeighborhoodId = neighborhoodId;
        Neighborhood = neighborhoodId is null ? null : neighborhoodName;
    }

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if ((latitude is null) != (longitude is null))
        {
            throw new DomainException("Latitude and longitude must be set together.");
        }

        if (latitude is < -90m or > 90m)
        {
            throw new DomainException("The latitude must be between -90 and 90.");
        }

        if (longitude is < -180m or > 180m)
        {
            throw new DomainException("The longitude must be between -180 and 180.");
        }
    }

    // Seis decimales (unos 10 cm) es la precisión de la columna; así lo guardado y lo devuelto coinciden.
    private static decimal? Round(decimal? value) => value is null ? null : Math.Round(value.Value, 6);
}
