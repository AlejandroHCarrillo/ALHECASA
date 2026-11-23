using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Users;

public sealed record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    UserRole Role,
    Guid? AgencyId,
    string? AgencyName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role,
    Guid? AgencyId);

public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? AgencyId,
    bool IsActive);

public sealed record UserListQuery(
    string? Search = null,
    UserRole? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 10)
{
    public const int MaxPageSize = 100;

    /// <summary>Devuelve una copia con paginación acotada a valores seguros.</summary>
    public UserListQuery Normalize() => this with
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize)
    };
}
