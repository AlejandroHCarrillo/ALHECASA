namespace Alhecasa.Domain.Enums;

public enum UserRole
{
    /// <summary>Administrador de la plataforma (superadministrador).</summary>
    PlatformAdmin = 1,

    /// <summary>Administrador de una inmobiliaria.</summary>
    AgencyAdmin = 2,

    /// <summary>Cliente: anunciante particular o usuario de una inmobiliaria.</summary>
    Client = 3
}
