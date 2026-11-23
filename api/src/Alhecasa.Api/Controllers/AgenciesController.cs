using Alhecasa.Application.Agencies;
using Alhecasa.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

[ApiController]
[Route("api/agencies")]
[Authorize(Roles = RoleNames.UserManagers)]
public sealed class AgenciesController(IAgencyService agencyService) : ControllerBase
{
    /// <summary>Lista las inmobiliarias visibles para el usuario (todas para PlatformAdmin; la propia para AgencyAdmin).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AgencyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgencyDto>>> List(CancellationToken cancellationToken) =>
        Ok(await agencyService.ListAsync(cancellationToken));
}
