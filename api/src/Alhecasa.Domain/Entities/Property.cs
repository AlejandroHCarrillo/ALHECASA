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
    decimal? LandArea);

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

    public int? Bedrooms { get; private set; }

    public int? Bathrooms { get; private set; }

    /// <summary>Superficie construida en metros cuadrados.</summary>
    public decimal? ConstructionArea { get; private set; }

    /// <summary>Superficie del terreno en metros cuadrados.</summary>
    public decimal? LandArea { get; private set; }

    /// <summary>Máximo de imágenes por propiedad.</summary>
    public const int MaxImages = 10;

    private readonly List<PropertyImage> _images = [];

    /// <summary>Galería de la propiedad (la primera imagen es la portada). Solo está completa si se cargó con la propiedad.</summary>
    public IReadOnlyCollection<PropertyImage> Images => _images;

    public void Update(PropertyData data) => Apply(data);

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
        Bedrooms = data.Bedrooms;
        Bathrooms = data.Bathrooms;
        ConstructionArea = data.ConstructionArea;
        LandArea = data.LandArea;
    }
}
