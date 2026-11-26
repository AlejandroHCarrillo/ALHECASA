using Alhecasa.Application.Common;
using Alhecasa.Application.Inquiries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Alhecasa.Api.Controllers;

/// <summary>Solicitud de informes de un visitante sobre una propiedad publicada. No requiere sesión.</summary>
[ApiController]
[Route("api/public/properties/{propertyId:guid}/inquiries")]
[AllowAnonymous]
[EnableRateLimiting("inquiries")]
public sealed class PublicInquiriesController(IPublicInquiryService publicInquiries) : ControllerBase
{
    /// <summary>
    /// Registra la solicitud para la inmobiliaria dueña de la propiedad. No devuelve nada del registro:
    /// el visitante no puede consultarlo después. 404 si la propiedad no existe o no está publicada.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(Guid propertyId, CreateInquiryRequest request, CancellationToken cancellationToken)
    {
        await publicInquiries.SubmitAsync(propertyId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }
}