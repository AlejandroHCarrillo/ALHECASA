using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

public class User : BaseEntity
{
    private User()
    {
    }

    public User(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        UserRole role,
        Guid? agencyId)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PasswordHash = passwordHash;
        ApplyRoleAndAgency(role, agencyId);
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    /// <summary>Correo normalizado en minúsculas. Es único entre usuarios no eliminados.</summary>
    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public Guid? AgencyId { get; private set; }

    public Agency? Agency { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset? LastLoginAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public void Update(
        string firstName,
        string lastName,
        string email,
        UserRole role,
        Guid? agencyId,
        bool isActive)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        IsActive = isActive;
        ApplyRoleAndAgency(role, agencyId);
    }

    public void RegisterLogin(DateTimeOffset now) => LastLoginAt = now;

    /// <summary>
    /// Regla de dominio: PlatformAdmin no pertenece a una inmobiliaria;
    /// AgencyAdmin siempre pertenece a una; Client puede ser particular (sin inmobiliaria).
    /// </summary>
    private void ApplyRoleAndAgency(UserRole role, Guid? agencyId)
    {
        switch (role)
        {
            case UserRole.PlatformAdmin when agencyId is not null:
                throw new DomainException("A platform administrator cannot belong to an agency.");
            case UserRole.AgencyAdmin when agencyId is null:
                throw new DomainException("An agency administrator must belong to an agency.");
        }

        Role = role;
        AgencyId = agencyId;
    }
}
