using System.Security.Claims;
using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Auth;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Api.Services;

/// <summary>Resuelve el usuario autenticado desde los claims del JWT de la solicitud actual.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuid(ClaimNames.Subject);

    public Guid? AgencyId => ParseGuid(ClaimNames.AgencyId);

    public UserRole? Role =>
        IsAuthenticated && Enum.TryParse<UserRole>(Principal!.FindFirstValue(ClaimNames.Role), out var role) && Enum.IsDefined(role)
            ? role
            : null;

    public bool IsPlatformAdmin => Role == UserRole.PlatformAdmin;

    private Guid? ParseGuid(string claimType) =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(claimType), out var value) ? value : null;
}
