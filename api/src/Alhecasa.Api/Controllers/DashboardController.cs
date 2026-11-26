using Alhecasa.Application.Auth;
using Alhecasa.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

/// <summary>Resumen de actividad de la inmobiliaria (o del espacio del particular) para su página de inicio.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = RoleNames.AgencyStaff)]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Propiedades por estado (activas, borradores, pausadas, cerradas, destacadas) y solicitudes de informes por etapa.</summary>
    [HttpGet]
    [ProducesResponseType<AgencyDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgencyDashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetAsync(cancellationToken));
}