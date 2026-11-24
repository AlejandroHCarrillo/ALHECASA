using Alhecasa.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace Alhecasa.Api.Controllers;

/// <summary>
/// Sirve las imágenes de las propiedades. No requiere sesión: las etiquetas de imagen del navegador no envían el token,
/// y el identificador (un Guid) no se puede adivinar.
/// </summary>
[ApiController]
[Route("api/images")]
[AllowAnonymous]
[EnableRateLimiting("images")]
public sealed class ImagesController(IPublicPropertyService publicProperties) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var image = await publicProperties.OpenImageAsync(id, cancellationToken);
        if (image is null)
        {
            return NotFound();
        }

        // El id es único y la imagen nunca cambia (se reemplaza por otra con otro id): se puede cachear sin límite.
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(image.Content, image.ContentType);
    }
}
