using Alhecasa.Application.Auth;
using Alhecasa.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

/// <summary>Lo contratado por cada cuenta (planes de publicaciones y paquetes de destacados) y lo que usa.</summary>
[ApiController]
[Route("api/billing")]
public sealed class BillingController(IBillingService billingService) : ControllerBase
{
    /// <summary>El plan de la cuenta del usuario y lo que lleva usado. Personal de una inmobiliaria o particular.</summary>
    [HttpGet("me")]
    [Authorize(Roles = RoleNames.AgencyStaff)]
    [ProducesResponseType<AccountBillingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccountBillingDto>> GetMine(CancellationToken cancellationToken) =>
        Ok(await billingService.GetMineAsync(cancellationToken));

    /// <summary>Los planes que la cuenta puede comprar. Administrador de la inmobiliaria o del espacio del particular.</summary>
    [HttpGet("plans")]
    [Authorize(Roles = RoleNames.AgencyAdmin)]
    [ProducesResponseType<IReadOnlyList<PlanDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> ListAvailablePlans(CancellationToken cancellationToken) =>
        Ok(await billingService.ListAvailablePlansAsync(cancellationToken));

    /// <summary>
    /// La cuenta compra un plan con tarjeta: se cobra el precio del plan en la pasarela y, si se aprueba, queda contratado
    /// (reemplaza al vigente del mismo tipo). Hoy la pasarela es una simulación. Administrador de la inmobiliaria.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Roles = RoleNames.AgencyAdmin)]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var created = await billingService.CheckoutAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Lo contratado y lo usado por todas las cuentas. Administrador de plataforma.</summary>
    [HttpGet("accounts")]
    [Authorize(Roles = RoleNames.PlatformAdmin)]
    [ProducesResponseType<IReadOnlyList<AccountBillingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountBillingDto>>> ListAccounts(CancellationToken cancellationToken) =>
        Ok(await billingService.ListAccountsAsync(cancellationToken));

    /// <summary>Historial de contrataciones de una cuenta, la más reciente primero. Administrador de plataforma.</summary>
    [HttpGet("accounts/{agencyId:guid}/subscriptions")]
    [Authorize(Roles = RoleNames.PlatformAdmin)]
    [ProducesResponseType<IReadOnlyList<SubscriptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionDto>>> ListSubscriptions(Guid agencyId, CancellationToken cancellationToken) =>
        Ok(await billingService.ListSubscriptionsAsync(agencyId, cancellationToken));

    /// <summary>
    /// Registra que la cuenta contrató un plan: empieza ahora y dura un periodo. Reemplaza la contratación vigente del mismo tipo.
    /// Administrador de plataforma.
    /// </summary>
    [HttpPost("accounts/{agencyId:guid}/subscriptions")]
    [Authorize(Roles = RoleNames.PlatformAdmin)]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> Assign(Guid agencyId, AssignPlanRequest request, CancellationToken cancellationToken)
    {
        var created = await billingService.AssignAsync(agencyId, request, cancellationToken);
        return CreatedAtAction(nameof(ListSubscriptions), new { agencyId }, created);
    }

    /// <summary>Termina una contratación ahora. Administrador de plataforma.</summary>
    [HttpPost("accounts/{agencyId:guid}/subscriptions/{subscriptionId:guid}/cancel")]
    [Authorize(Roles = RoleNames.PlatformAdmin)]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionDto>> Cancel(Guid agencyId, Guid subscriptionId, CancellationToken cancellationToken) =>
        Ok(await billingService.CancelAsync(agencyId, subscriptionId, cancellationToken));
}
