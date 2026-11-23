using System.Security.Claims;
using Alhecasa.Application.Auth;
using Alhecasa.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Alhecasa.Api.Authentication;

/// <summary>
/// Revalida cada token contra la base de datos tras verificar su firma: la cuenta debe existir,
/// estar activa y conservar el rol y la inmobiliaria con los que se emitió el token.
/// Cuesta una consulta por solicitud autenticada; es el precio de revocar el acceso al instante.
/// </summary>
internal static class SessionRevalidation
{
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        if (!TryReadClaims(context.Principal, out var userId, out var role, out var agencyId))
        {
            context.Fail("The token does not contain valid identity claims.");
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
        var isValid = await validator.IsValidAsync(userId, role, agencyId, context.HttpContext.RequestAborted);

        if (!isValid)
        {
            context.Fail("The account is no longer valid for this token.");
        }
    }

    private static bool TryReadClaims(ClaimsPrincipal? principal, out Guid userId, out UserRole role, out Guid? agencyId)
    {
        userId = Guid.Empty;
        role = default;
        agencyId = null;

        if (principal is null
            || !Guid.TryParse(principal.FindFirstValue(ClaimNames.Subject), out userId)
            || !Enum.TryParse(principal.FindFirstValue(ClaimNames.Role), out role)
            || !Enum.IsDefined(role))
        {
            return false;
        }

        var agencyClaim = principal.FindFirstValue(ClaimNames.AgencyId);
        if (agencyClaim is null)
        {
            return true;
        }

        if (!Guid.TryParse(agencyClaim, out var parsed))
        {
            return false;
        }

        agencyId = parsed;
        return true;
    }
}
