using Alhecasa.Application.Auth;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

[ApiController]
[Route("api/properties")]
[Authorize(Roles = RoleNames.PropertyManagers)]
public sealed class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    // 10 imágenes de 5 MB más el sobrante del formato multipart.
    private const long MaxRequestBytes = (Alhecasa.Domain.Entities.Property.MaxImages * PropertyImageRules.MaxBytes) + (1024 * 1024);

    /// <summary>Lista el inventario de propiedades con búsqueda, filtros (estado, tipo, operación, precio, recámaras…), orden y paginación.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PropertyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PropertyDto>>> GetPaged([FromQuery] PropertyListQuery query, CancellationToken cancellationToken) =>
        Ok(await propertyService.GetPagedAsync(query, cancellationToken));

    /// <summary>
    /// Galería de la inmobiliaria: sus propiedades publicadas tal como las ve un visitante (mismos datos públicos, mismo orden).
    /// Solo para personal de una inmobiliaria.
    /// </summary>
    [HttpGet("gallery")]
    [Authorize(Roles = RoleNames.AgencyStaff)]
    [ProducesResponseType<PagedResult<PublicPropertyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<PublicPropertyDto>>> GetGallery([FromQuery] PublicPropertyQuery query, CancellationToken cancellationToken = default) =>
        Ok(await propertyService.GetGalleryAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await propertyService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        var created = await propertyService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyDto>> Update(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken) =>
        Ok(await propertyService.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Cambia la galería de la propiedad en una sola operación (multipart/form-data): <c>files</c> trae las imágenes nuevas
    /// y <c>removeImageIds</c> los identificadores de las que se quitan. Máximo 10 imágenes de hasta 5 MB (JPEG, PNG o WebP);
    /// una propiedad publicada necesita al menos una.
    /// </summary>
    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<PropertyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyDto>> UpdateImages(Guid id, [FromForm] PropertyImagesForm form, CancellationToken cancellationToken)
    {
        var streams = new List<Stream>();
        try
        {
            var uploads = (form.Files as IEnumerable<IFormFile> ?? []).Select(file =>
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                return new ImageUpload(stream, file.Length);
            }).ToList();

            return Ok(await propertyService.UpdateImagesAsync(id, uploads, form.RemoveImageIds ?? [], cancellationToken));
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>Eliminación lógica.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await propertyService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
