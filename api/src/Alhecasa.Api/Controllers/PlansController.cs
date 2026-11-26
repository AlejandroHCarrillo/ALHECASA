using Alhecasa.Application.Auth;
using Alhecasa.Application.Billing;
using Alhecasa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

/// <summary>Catálogo de planes para monetizar el portal. Solo el administrador de plataforma.</summary>
[ApiController]
[Route("api/plans")]
[Authorize(Roles = RoleNames.PlatformAdmin)]
public sealed class PlansController(IPlanService planService) : ControllerBase
{
    /// <summary>Lista los planes (activos e inactivos), opcionalmente de un solo tipo (<c>Listings</c> o <c>Featured</c>).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlanDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> List([FromQuery] PlanKind? kind, CancellationToken cancellationToken) =>
        Ok(await planService.ListAsync(kind, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await planService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PlanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanDto>> Create(SavePlanRequest request, CancellationToken cancellationToken)
    {
        var created = await planService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanDto>> Update(Guid id, SavePlanRequest request, CancellationToken cancellationToken) =>
        Ok(await planService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Eliminación lógica. El plan gratuito no se puede eliminar. Las contrataciones que ya existen no cambian.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await planService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
