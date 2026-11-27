using System.Text;
using Alhecasa.Application.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Alhecasa.Api.Controllers;

/// <summary>Zonas públicas (ciudades y colonias con propiedades publicadas) y el mapa del sitio para los buscadores. No requiere sesión.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class PublicZonesController(IZoneService zones, ISitemapService sitemap) : ControllerBase
{
    /// <summary>Las zonas con más propiedades publicadas (ciudades y colonias), de la más a la menos poblada. Hasta 100.</summary>
    [HttpGet("zones")]
    [ProducesResponseType<IReadOnlyList<ZoneLinkDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ZoneLinkDto>>> GetTop([FromQuery] int limit = 30, CancellationToken cancellationToken = default) =>
        Ok(await zones.GetTopAsync(limit, cancellationToken));

    /// <summary>Una ciudad (por su identificador, p. ej. <c>guadalajara</c>) con sus colonias y cuántas propiedades hay en venta y en renta. 404 si no existe o no tiene ninguna publicada.</summary>
    [HttpGet("zones/{citySlug}")]
    [ProducesResponseType<ZoneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZoneDto>> GetCity(string citySlug, CancellationToken cancellationToken) =>
        Ok(await zones.GetAsync(citySlug, null, cancellationToken));

    /// <summary>Una colonia de una ciudad. 404 si no existe o no tiene ninguna propiedad publicada.</summary>
    [HttpGet("zones/{citySlug}/{neighborhoodSlug}")]
    [ProducesResponseType<ZoneDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZoneDto>> GetNeighborhood(string citySlug, string neighborhoodSlug, CancellationToken cancellationToken) =>
        Ok(await zones.GetAsync(citySlug, neighborhoodSlug, cancellationToken));

    /// <summary>
    /// Mapa del sitio (sitemaps.org): la portada, las zonas con propiedades en venta o renta, las inmobiliarias y las publicaciones.
    /// Las direcciones llevan el sitio configurado en <c>Seo:SiteUrl</c>.
    /// </summary>
    [HttpGet("sitemap.xml")]
    [Produces("application/xml")]
    public async Task<ContentResult> GetSitemap(CancellationToken cancellationToken) =>
        Content(await sitemap.BuildAsync(cancellationToken), "application/xml", Encoding.UTF8);
}
