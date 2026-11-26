using Alhecasa.Domain.Common;

namespace Alhecasa.Domain.Entities;

/// <summary>
/// Catálogo de ubicaciones: estados, ciudades y colonias, con un identificador (slug) estable que forma las direcciones
/// públicas por zona (<c>/venta/guadalajara/providencia</c>) y une las distintas escrituras de un mismo nombre.
/// Es un catálogo de la plataforma, no de una inmobiliaria: no está aislado por inmobiliaria.
/// </summary>
public static class GeoLimits
{
    public const int Name = 100;

    public const int Slug = 210;
}

/// <summary>Estado (entidad federativa).</summary>
public class GeoState : BaseEntity
{
    private GeoState()
    {
    }

    public GeoState(string name)
    {
        Name = GeoNaming.Clean(name, "state");
        Slug = Common.Slug.From(Name);
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Identificador único del estado (sin acentos, en minúsculas).</summary>
    public string Slug { get; private set; } = string.Empty;
}

/// <summary>Ciudad o municipio de un estado.</summary>
public class GeoCity : BaseEntity
{
    private GeoCity()
    {
    }

    /// <param name="slug">Identificador único de la ciudad en toda la plataforma (si el nombre ya existe en otro estado, lleva el del estado).</param>
    public GeoCity(Guid stateId, string name, string slug)
    {
        if (stateId == Guid.Empty)
        {
            throw new DomainException("A city must belong to a state.");
        }

        StateId = stateId;
        Name = GeoNaming.Clean(name, "city");
        Slug = string.IsNullOrWhiteSpace(slug) ? throw new DomainException("The city needs a slug.") : slug;
    }

    public Guid StateId { get; private set; }

    public GeoState? State { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;
}

/// <summary>Colonia de una ciudad.</summary>
public class GeoNeighborhood : BaseEntity
{
    private GeoNeighborhood()
    {
    }

    public GeoNeighborhood(Guid cityId, string name)
    {
        if (cityId == Guid.Empty)
        {
            throw new DomainException("A neighborhood must belong to a city.");
        }

        CityId = cityId;
        Name = GeoNaming.Clean(name, "neighborhood");
        Slug = Common.Slug.From(Name);
    }

    public Guid CityId { get; private set; }

    public GeoCity? City { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Identificador de la colonia, único dentro de su ciudad.</summary>
    public string Slug { get; private set; } = string.Empty;
}

internal static class GeoNaming
{
    public static string Clean(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"The {what} name is required.");
        }

        var name = LocationNames.Tidy(value);
        if (name.Length > GeoLimits.Name)
        {
            throw new DomainException($"The {what} name is too long.");
        }

        if (Common.Slug.From(name).Length == 0)
        {
            throw new DomainException($"The {what} name must contain letters or numbers.");
        }

        return name;
    }
}
