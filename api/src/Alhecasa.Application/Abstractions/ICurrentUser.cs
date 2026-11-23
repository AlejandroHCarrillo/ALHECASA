using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Abstractions;

/// <summary>
/// Usuario autenticado de la solicitud actual (se resuelve desde el JWT en la capa API).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    UserRole? Role { get; }

    Guid? AgencyId { get; }

    bool IsPlatformAdmin { get; }
}
