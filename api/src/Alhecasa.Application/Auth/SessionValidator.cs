using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Auth;

/// <summary>Estado actual, en base de datos, de la cuenta a la que pertenece un token.</summary>
public sealed record UserSessionState(UserRole Role, Guid? AgencyId, bool IsActive, bool AgencyIsUsable);

/// <summary>
/// Comprueba que un token emitido sigue siendo válido frente al estado actual de la cuenta.
/// Los permisos viajan en los claims del JWT; sin esta revalidación, un usuario desactivado,
/// eliminado o con otro rol conservaría el acceso hasta que expirara el token.
/// </summary>
public interface ISessionValidator
{
    Task<bool> IsValidAsync(Guid userId, UserRole role, Guid? agencyId, CancellationToken cancellationToken = default);
}

public sealed class SessionValidator(IUserRepository users) : ISessionValidator
{
    public async Task<bool> IsValidAsync(
        Guid userId,
        UserRole role,
        Guid? agencyId,
        CancellationToken cancellationToken = default)
    {
        var state = await users.GetSessionStateAsync(userId, cancellationToken);

        return state is { IsActive: true, AgencyIsUsable: true }
            && state.Role == role
            && state.AgencyId == agencyId;
    }
}
