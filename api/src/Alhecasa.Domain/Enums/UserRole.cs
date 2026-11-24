namespace Alhecasa.Domain.Enums;

public enum UserRole
{
    /// <summary>Administrador de la plataforma (superadministrador).</summary>
    PlatformAdmin = 1,

    /// <summary>Administrador de una inmobiliaria.</summary>
    AgencyAdmin = 2,

    /// <summary>Cliente: anunciante particular o usuario de una inmobiliaria.</summary>
    Client = 3,

    /// <summary>Supervisor: empleado de una inmobiliaria que coordina al equipo de vendedores.</summary>
    Supervisor = 4,

    /// <summary>Vendedor: empleado de una inmobiliaria que atiende y gestiona propiedades y clientes.</summary>
    Seller = 5
}
