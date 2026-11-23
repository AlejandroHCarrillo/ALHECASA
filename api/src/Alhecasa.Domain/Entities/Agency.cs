using Alhecasa.Domain.Common;

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

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; } = true;
}
