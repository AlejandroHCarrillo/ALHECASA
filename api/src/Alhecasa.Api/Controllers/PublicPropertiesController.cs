using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Alhecasa.Api.Controllers;

/// <summary>Catálogo público de propiedades, para la portada de visitantes. No requiere sesión.</summary>
[ApiController]
[Route("api/public/properties")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class PublicPropertiesController(IPublicPropertyService publicProperties) : ControllerBase
{
    /// <summary>Lista las propiedades publicadas, con filtros (operación, tipo, ubicación, precio, recámaras, baños, superficie, estacionamientos, mascotas, amueblado y amenidades), orden y paginación.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PublicPropertyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PublicPropertyDto>>> GetPublished([FromQuery] PublicPropertyQuery query, CancellationToken cancellationToken) =>
        Ok(await publicProperties.GetPublishedAsync(query, cancellationToken));

    /// <summary>Detalle de una propiedad publicada, con todas sus imágenes. 404 si no existe o no está publicada.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PublicPropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicPropertyDto>> GetPublishedById(Guid id, CancellationToken cancellationToken) =>
        Ok(await publicProperties.GetPublishedByIdAsync(id, cancellationToken));
}
