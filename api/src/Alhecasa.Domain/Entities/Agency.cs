using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

/// <summary>
/// Inmobiliaria (empresa). Es el límite de aislamiento de datos (tenant).
/// </summary>
public class Agency : BaseEntity
{
    private Agency()
    {
    }

    public Agency(string name, string email, string? phone)
    {
        Name = name;
        Email = email;
        Phone = phone;
    }

    /// <summary>
    /// Cuenta de un particular. Es un espacio propio (tenant) con un solo usuario, para que sus propiedades
    /// y solicitudes queden aisladas igual que las de una inmobiliaria.
    /// </summary>
    public static Agency CreateIndividual(string name, string email) =>
        new(name, email, null) { Kind = AgencyKind.Individual };

    public AgencyKind Kind { get; private set; } = AgencyKind.Company;

    public bool IsIndividual => Kind == AgencyKind.Individual;

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ---- Perfil que la inmobiliaria configura y que se muestra en su página pública ----

    public string? Description { get; private set; }

    public string? Website { get; private set; }

    public string? FacebookUrl { get; private set; }

    public string? InstagramUrl { get; private set; }

    /// <summary>Horario de atención, en texto libre (p. ej. «Lun a Vie 9:00 a 18:00»).</summary>
    public string? OpeningHours { get; private set; }

    public string? Street { get; private set; }

    public string? City { get; private set; }

    public string? State { get; private set; }

    public string? PostalCode { get; private set; }

    /// <summary>Clave del archivo del logotipo en el almacenamiento, o null si no tiene.</summary>
    public string? LogoStorageKey { get; private set; }

    public string? LogoContentType { get; private set; }

    public bool HasLogo => LogoStorageKey is not null;

    /// <summary>Reemplaza los datos del perfil. Los textos vacíos se guardan como «sin dato».</summary>
    public void UpdateProfile(AgencyProfile profile)
    {
        Name = profile.Name.Trim();
        Email = profile.Email.Trim();
        Phone = Clean(profile.Phone);
        Description = Clean(profile.Description);
        Website = Clean(profile.Website);
        FacebookUrl = Clean(profile.FacebookUrl);
        InstagramUrl = Clean(profile.InstagramUrl);
        OpeningHours = Clean(profile.OpeningHours);
        Street = Clean(profile.Street);
        City = Clean(profile.City);
        State = Clean(profile.State);
        PostalCode = Clean(profile.PostalCode);
    }

    /// <summary>Fija el logotipo nuevo y devuelve la clave del anterior (para borrar su archivo), o null si no había.</summary>
    public string? SetLogo(string storageKey, string contentType)
    {
        var previous = LogoStorageKey;
        LogoStorageKey = storageKey;
        LogoContentType = contentType;
        return previous;
    }

    /// <summary>Quita el logotipo y devuelve la clave del archivo que tenía, o null si no había.</summary>
    public string? RemoveLogo()
    {
        var previous = LogoStorageKey;
        LogoStorageKey = null;
        LogoContentType = null;
        return previous;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Datos editables del perfil de una inmobiliaria.</summary>
public sealed record AgencyProfile(
    string Name,
    string Email,
    string? Phone,
    string? Description,
    string? Website,
    string? FacebookUrl,
    string? InstagramUrl,
    string? OpeningHours,
    string? Street,
    string? City,
    string? State,
    string? PostalCode);

/// <summary>Longitudes máximas del perfil. Las usan la validación de la API y el esquema de la base.</summary>
public static class AgencyProfileLimits
{
    public const int Name = 200;
    public const int Email = 256;
    public const int Phone = 30;
    public const int Description = 1000;
    public const int Url = 200;
    public const int OpeningHours = 200;
    public const int Street = 200;
    public const int City = 100;
    public const int State = 100;
    public const int PostalCode = 10;
}
