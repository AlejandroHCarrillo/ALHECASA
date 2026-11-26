using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Users;

/// <summary>
/// Reglas de autorización:
/// - PlatformAdmin: administra usuarios de cualquier inmobiliaria (y particulares).
/// - AgencyAdmin: solo usuarios de su inmobiliaria (incluidos sus empleados Supervisor y Seller); no puede crear PlatformAdmin ni Client.
/// - Supervisor, Seller y Client: sin acceso a la administración de usuarios.
/// - AgencyAdmin de una cuenta gratuita de particular (AgencyKind.Individual): sin acceso; solo tiene su propio usuario.
/// El aislamiento de lectura lo garantiza además el filtro global del repositorio.
/// </summary>
public sealed class UserService(
    IUserRepository users,
    IAgencyRepository agencies,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator,
    TimeProvider timeProvider) : IUserService
{
    public async Task<PagedResult<UserDto>> GetPagedAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageUsersAsync(cancellationToken);
        var page = await users.GetPagedAsync(query.Normalize(), cancellationToken);
        return page.Map(ToDto);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageUsersAsync(cancellationToken);
        var user = await users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");
        return ToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageUsersAsync(cancellationToken);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        EnsureCanAssignRole(request.Role, currentRole: null);
        var agencyId = await ResolveAgencyIdAsync(request.Role, request.AgencyId, cancellationToken);
        var email = EmailNormalizer.Normalize(request.Email);

        if (await users.EmailExistsAsync(email, null, cancellationToken))
        {
            throw new ConflictException("The email is already registered.");
        }

        var user = new User(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            email,
            passwordHasher.Hash(request.Password),
            request.Role,
            agencyId);

        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageUsersAsync(cancellationToken);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        if (user.Id == currentUser.UserId && (request.Role != user.Role || !request.IsActive))
        {
            throw new ForbiddenException("You cannot change your own role or deactivate your own account.");
        }

        // Un AgencyAdmin no puede modificar a un PlatformAdmin (el filtro ya lo oculta, es defensa en profundidad).
        if (!currentUser.IsPlatformAdmin && user.Role == UserRole.PlatformAdmin)
        {
            throw new ForbiddenException("You cannot modify a platform administrator.");
        }

        EnsureCanAssignRole(request.Role, user.Role);
        var agencyId = await ResolveAgencyIdAsync(request.Role, request.AgencyId, cancellationToken);
        var email = EmailNormalizer.Normalize(request.Email);

        if (await users.EmailExistsAsync(email, user.Id, cancellationToken))
        {
            throw new ConflictException("The email is already registered.");
        }

        user.Update(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            email,
            request.Role,
            agencyId,
            request.IsActive);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageUsersAsync(cancellationToken);

        if (id == currentUser.UserId)
        {
            throw new ForbiddenException("You cannot delete your own account.");
        }

        var user = await users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        if (!currentUser.IsPlatformAdmin && user.Role == UserRole.PlatformAdmin)
        {
            throw new ForbiddenException("You cannot delete a platform administrator.");
        }

        user.MarkAsDeleted(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCanManageUsersAsync(CancellationToken cancellationToken)
    {
        if (currentUser.Role is not (UserRole.PlatformAdmin or UserRole.AgencyAdmin))
        {
            throw new ForbiddenException("You do not have permission to manage users.");
        }

        // La cuenta gratuita de un particular tiene un solo usuario: no administra personal.
        if (currentUser.Role == UserRole.AgencyAdmin
            && currentUser.AgencyId is { } agencyId
            && await agencies.GetByIdAsync(agencyId, cancellationToken) is { IsIndividual: true })
        {
            throw new ForbiddenException("A free individual account cannot manage users.");
        }
    }

    /// <summary>
    /// Un AgencyAdmin administra a su personal, no da de alta clientes: no puede crear un Client
    /// ni convertir a otro usuario en Client. Sí puede seguir editando a un Client que ya existe sin cambiarle el rol.
    /// </summary>
    private void EnsureCanAssignRole(UserRole targetRole, UserRole? currentRole)
    {
        if (!currentUser.IsPlatformAdmin && targetRole == UserRole.Client && currentRole != UserRole.Client)
        {
            throw new ForbiddenException("You cannot create client users.");
        }
    }

    /// <summary>
    /// Determina la inmobiliaria final del usuario según el rol de quien opera
    /// y valida que la inmobiliaria exista.
    /// </summary>
    private async Task<Guid?> ResolveAgencyIdAsync(UserRole targetRole, Guid? requestedAgencyId, CancellationToken cancellationToken)
    {
        Guid? agencyId;

        if (currentUser.IsPlatformAdmin)
        {
            agencyId = requestedAgencyId;
        }
        else
        {
            if (targetRole == UserRole.PlatformAdmin)
            {
                throw new ForbiddenException("You cannot assign the platform administrator role.");
            }

            if (requestedAgencyId is not null && requestedAgencyId != currentUser.AgencyId)
            {
                throw new ForbiddenException("You cannot assign users to another agency.");
            }

            agencyId = currentUser.AgencyId;
        }

        if (targetRole == UserRole.PlatformAdmin)
        {
            agencyId = null;
        }

        if (agencyId is not null && await agencies.GetByIdAsync(agencyId.Value, cancellationToken) is null)
        {
            throw new NotFoundException("Agency not found.");
        }

        return agencyId;
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.FullName,
        user.Email,
        user.Role,
        user.AgencyId,
        user.Agency?.Name,
        user.IsActive,
        user.CreatedAt,
        user.LastLoginAt);
}
