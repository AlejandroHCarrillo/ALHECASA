using Alhecasa.Application.Agencies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Alhecasa.Api.Controllers;

/// <summary>Página pública de una inmobiliaria: su información y su logotipo. No requiere sesión.</summary>
[ApiController]
[Route("api/public/agencies")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class PublicAgenciesController(IPublicAgencyService publicAgencies) : ControllerBase
{
    /// <summary>Información pública de una inmobiliaria. 404 si no existe, está inactiva o es un particular.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PublicAgencyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicAgencyDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await publicAgencies.GetAsync(id, cancellationToken));

    /// <summary>Logotipo de una inmobiliaria. 404 si no existe, no es visible o no tiene logotipo.</summary>
    [HttpGet("{id:guid}/logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(Guid id, CancellationToken cancellationToken)
    {
        var logo = await publicAgencies.OpenLogoAsync(id, cancellationToken);
        if (logo is null)
        {
            return NotFound();
        }

        // El logotipo puede cambiar en la misma dirección: el navegador lo revalida con el ETag (sale 304 si no cambió).
        Response.Headers[HeaderNames.CacheControl] = "public, no-cache";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(logo.Content, logo.ContentType, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{logo.ETag}\""));
    }
}
