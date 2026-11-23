using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Users;
using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Obtiene un usuario respetando el aislamiento por inmobiliaria y el borrado lógico.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<User>> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca por correo ignorando el aislamiento por inmobiliaria (se usa en el login,
    /// cuando todavía no existe un usuario autenticado). Excluye usuarios eliminados.
    /// </summary>
    Task<User?> GetByEmailForAuthenticationAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estado actual de la cuenta para revalidar un token. Ignora el aislamiento por inmobiliaria
    /// porque se consulta mientras el usuario de la solicitud todavía no está establecido.
    /// Devuelve null si el usuario no existe o fue eliminado.
    /// </summary>
    Task<UserSessionState?> GetSessionStateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Unicidad global del correo, independiente de la inmobiliaria.</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
