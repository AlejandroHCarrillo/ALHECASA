using Alhecasa.Application.Agencies;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

[ApiController]
[Route("api/agencies")]
[Authorize(Roles = RoleNames.UserManagers)]
public sealed class AgenciesController(IAgencyService agencyService) : ControllerBase
{
    // El logotipo pesa hasta 5 MB más el sobrante del formato multipart.
    private const long LogoRequestBytes = PropertyImageRules.MaxBytes + (1024 * 1024);

    /// <summary>Lista las inmobiliarias visibles para el usuario (todas para PlatformAdmin; la propia para AgencyAdmin).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AgencyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgencyDto>>> List(CancellationToken cancellationToken) =>
        Ok(await agencyService.ListAsync(cancellationToken));

    /// <summary>Perfil de la propia inmobiliaria. Solo su administrador (no los particulares).</summary>
    [HttpGet("me")]
    [Authorize(Roles = RoleNames.AgencyAdmin)]
    [ProducesResponseType<AgencyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgencyProfileDto>> GetMine(CancellationToken cancellationToken) =>
        Ok(await agencyService.GetMineAsync(cancellationToken));

    /// <summary>Reemplaza los datos del perfil de la propia inmobiliaria. Las direcciones web deben ser http o https.</summary>
    [HttpPut("me")]
    [Authorize(Roles = RoleNames.AgencyAdmin)]
    [ProducesResponseType<AgencyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgencyProfileDto>> UpdateMine(UpdateAgencyProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await agencyService.UpdateMineAsync(request, cancellationToken));

    /// <summary>Sube o reemplaza el logotipo (campo <c>file</c>): JPEG, PNG o WebP de hasta 5 MB.</summary>
    [HttpPut("me/logo")]
    [Authorize(Roles = RoleNames.AgencyAdmin)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LogoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = LogoRequestBytes)]
    [ProducesResponseType<AgencyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgencyProfileDto>> SetLogo(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Business rule violated", Detail = "An image file is required." });
        }

        await using var stream = file.OpenReadStream();
        return Ok(await agencyService.SetLogoAsync(new ImageUpload(stream, file.Length), cancellationToken));
    }

    /// <summary>Quita el logotipo de la propia inmobiliaria.</summary>
    [HttpDelete("me/logo")]
    [Authorize(Roles = RoleNames.AgencyAdmin)]
    [ProducesResponseType<AgencyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgencyProfileDto>> RemoveLogo(CancellationToken cancellationToken) =>
        Ok(await agencyService.RemoveLogoAsync(cancellationToken));
}
