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
}
