using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Properties;

/// <summary>Orden de los resultados. Siempre se desempata por antigüedad y luego por id, para que el orden sea estable.</summary>
public enum PropertySort
{
    /// <summary>Las más recientes primero (el valor por omisión).</summary>
    Recent = 0,

    PriceAsc = 1,

    PriceDesc = 2,

    /// <summary>Mayor superficie primero (la construida; en un terreno, la del terreno).</summary>
    AreaDesc = 3
}

/// <summary>
/// Criterios de búsqueda que comparten el catálogo público y la lista del personal, para que no diverjan.
/// Todos son opcionales y se combinan con «y».
/// </summary>
public interface IPropertyCriteria
{
    PropertyType? Type { get; }

    OperationType? Operation { get; }

    /// <summary>Precio mínimo y máximo, en <see cref="Currency"/>. No hay conversión entre monedas.</summary>
    decimal? MinPrice { get; }

    decimal? MaxPrice { get; }

    /// <summary>Moneda de los límites de precio. Es obligatoria si se envía alguno.</summary>
    CurrencyCode? Currency { get; }

    int? MinBedrooms { get; }

    int? MinBathrooms { get; }

    int? MinParking { get; }

    /// <summary>Superficie en m²: la construida y, en una propiedad sin construcción (un terreno), la del terreno.</summary>
    decimal? MinArea { get; }

    decimal? MaxArea { get; }

    /// <summary>Si es verdadero, solo las que aceptan mascotas. Falso o nulo no filtra.</summary>
    bool? PetsAllowed { get; }

    /// <summary>Si es verdadero, solo las amuebladas. Falso o nulo no filtra.</summary>
    bool? Furnished { get; }

    /// <summary>Deben tener todas las amenidades indicadas.</summary>
    IReadOnlyList<Amenity>? Amenities { get; }

    PropertySort Sort { get; }
}

/// <summary>Validación de los criterios: un valor absurdo responde 400 en lugar de devolver una lista vacía sin explicación.</summary>
internal static class PropertyCriteriaRules
{
    public const int MaxCount = 50;

    public static void Validate(IPropertyCriteria criteria)
    {
        if (criteria.Type is { } type && !Enum.IsDefined(type))
        {
            throw new DomainException("The property type is not valid.");
        }

        if (criteria.Operation is { } operation && !Enum.IsDefined(operation))
        {
            throw new DomainException("The operation is not valid.");
        }

        if (criteria.Currency is { } currency && !Enum.IsDefined(currency))
        {
            throw new DomainException("The currency is not valid.");
        }

        if (!Enum.IsDefined(criteria.Sort))
        {
            throw new DomainException("The sort order is not valid.");
        }

        if ((criteria.MinPrice is not null || criteria.MaxPrice is not null) && criteria.Currency is null)
        {
            throw new DomainException("A currency is required to filter by price.");
        }

        CheckRange(criteria.MinPrice, criteria.MaxPrice, PropertyRequestValidator<CreatePropertyRequest>.MaxPrice, "price");
        CheckRange(criteria.MinArea, criteria.MaxArea, PropertyRequestValidator<CreatePropertyRequest>.MaxArea, "area");

        CheckCount(criteria.MinBedrooms, "bedrooms");
        CheckCount(criteria.MinBathrooms, "bathrooms");
        CheckCount(criteria.MinParking, "parking spaces");

        if (criteria.Amenities?.Any(a => !AmenityFlags.IsSingle(a)) == true)
        {
            throw new DomainException("An amenity is not valid.");
        }
    }

    private static void CheckRange(decimal? min, decimal? max, decimal limit, string name)
    {
        if (min is < 0 || max is < 0 || min > limit || max > limit)
        {
            throw new DomainException($"The {name} range is not valid.");
        }

        if (min is not null && max is not null && min > max)
        {
            throw new DomainException($"The minimum {name} cannot be greater than the maximum.");
        }
    }

    private static void CheckCount(int? value, string name)
    {
        if (value is < 0 or > MaxCount)
        {
            throw new DomainException($"The minimum number of {name} must be between 0 and {MaxCount}.");
        }
    }

    /// <summary>Lista sin repetidos y sin vacíos, o null si no queda nada (para que «sin amenidades» no filtre).</summary>
    public static IReadOnlyList<Amenity>? NormalizeAmenities(IReadOnlyList<Amenity>? amenities)
    {
        var distinct = amenities?.Distinct().ToList();
        return distinct is { Count: > 0 } ? distinct : null;
    }
}
