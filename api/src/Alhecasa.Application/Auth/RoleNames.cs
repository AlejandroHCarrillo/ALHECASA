namespace Alhecasa.Application.Auth;

/// <summary>
/// Nombres de rol usados en los claims del JWT y en los atributos [Authorize].
/// Deben coincidir con los nombres del enum <see cref="Domain.Enums.UserRole"/>.
/// </summary>
public static class RoleNames
{
    public const string PlatformAdmin = nameof(Domain.Enums.UserRole.PlatformAdmin);
    public const string AgencyAdmin = nameof(Domain.Enums.UserRole.AgencyAdmin);
    public const string Client = nameof(Domain.Enums.UserRole.Client);

    /// <summary>Roles con permiso de administrar usuarios.</summary>
    public const string UserManagers = PlatformAdmin + "," + AgencyAdmin;
}
