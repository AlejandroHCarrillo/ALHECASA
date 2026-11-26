using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Billing;

/// <summary>
/// Lo que una cuenta puede hacer según su plan. Reglas de publicaciones:
/// 1) con un plan de publicaciones vigente, su tope; 2) un particular sin plan de pago tiene el plan gratuito (3 propiedades por omisión);
/// 3) una inmobiliaria sin plan contratado no tiene tope (así funcionan las inmobiliarias que ya existían; contratar un plan lo pone).
/// Los destacados solo existen mientras haya un paquete de destacados vigente.
/// </summary>
public interface IEntitlementService
{
    Task<ListingsAllowanceDto> GetListingsAsync(Agency agency, CancellationToken cancellationToken = default);

    /// <summary>El paquete de destacados vigente de la cuenta, o null si no tiene.</summary>
    Task<FeaturedAllowanceDto?> GetFeaturedAsync(Guid agencyId, CancellationToken cancellationToken = default);
}

public sealed class EntitlementService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IPropertyRepository properties,
    TimeProvider timeProvider,
    AccountLimits accountLimits) : IEntitlementService
{
    public async Task<ListingsAllowanceDto> GetListingsAsync(Agency agency, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var subscription = await subscriptions.GetActiveAsync(agency.Id, PlanKind.Listings, now, cancellationToken);
        var free = subscription is null && agency.IsIndividual ? await plans.GetActiveFreeAsync(cancellationToken) : null;
        var used = await properties.CountByAgencyAsync(agency.Id, cancellationToken);

        return Allowances.Listings(agency, subscription, free, used, accountLimits);
    }

    public async Task<FeaturedAllowanceDto?> GetFeaturedAsync(Guid agencyId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var subscription = await subscriptions.GetActiveAsync(agencyId, PlanKind.Featured, now, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var used = (await properties.ListFeaturedAsync(agencyId, now, cancellationToken)).Count;
        return Allowances.Featured(subscription, used);
    }
}

/// <summary>Cálculo puro de lo permitido, compartido por la consulta de una cuenta y por el tablero de todas.</summary>
internal static class Allowances
{
    public const string FreePlanFallbackName = "Gratuito";

    public static ListingsAllowanceDto Listings(Agency agency, Subscription? subscription, Plan? freePlan, int used, AccountLimits limits)
    {
        if (subscription is not null)
        {
            return new ListingsAllowanceDto(subscription.PlanName, subscription.Limit, used, subscription.EndsAt, false);
        }

        if (agency.IsIndividual)
        {
            // Sin plan gratuito en el catálogo, rige el tope de configuración.
            return new ListingsAllowanceDto(
                freePlan?.Name ?? FreePlanFallbackName,
                freePlan is null ? limits.IndividualMaxProperties : freePlan.Limit,
                used,
                null,
                true);
        }

        return new ListingsAllowanceDto(null, null, used, null, false);
    }

    public static FeaturedAllowanceDto Featured(Subscription subscription, int used) =>
        new(subscription.PlanName, subscription.Limit, used, subscription.EndsAt);
}

public interface IBillingService
{
    /// <summary>Lo contratado y lo usado por todas las cuentas (administrador de plataforma).</summary>
    Task<IReadOnlyList<AccountBillingDto>> ListAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lo contratado y lo usado por la cuenta del usuario (personal de una inmobiliaria o particular).</summary>
    Task<AccountBillingDto> GetMineAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionDto>> ListSubscriptionsAsync(Guid agencyId, CancellationToken cancellationToken = default);

    /// <summary>Registra que la cuenta contrató un plan: empieza ahora y dura un periodo. Reemplaza la contratación vigente del mismo tipo.</summary>
    Task<SubscriptionDto> AssignAsync(Guid agencyId, AssignPlanRequest request, CancellationToken cancellationToken = default);

    Task<SubscriptionDto> CancelAsync(Guid agencyId, Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Los planes que la cuenta del usuario puede comprar: activos, de pago y para su tipo de cuenta. Administrador de la inmobiliaria.</summary>
    Task<IReadOnlyList<PlanDto>> ListAvailablePlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// La cuenta compra un plan con tarjeta. Cobra el precio del plan en la pasarela y, si se aprueba, crea la contratación
    /// (que reemplaza a la vigente del mismo tipo). Si se rechaza, no cambia nada. Administrador de la inmobiliaria.
    /// </summary>
    Task<SubscriptionDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contrataciones de planes. Solo el administrador de plataforma las registra y las cancela (no hay cobro en línea todavía:
/// registra el pago que recibió). Cambiar o cancelar un paquete de destacados quita el destacado a los anuncios de la cuenta,
/// porque el nuevo paquete puede tener otra capacidad: la cuenta vuelve a elegir cuáles destacar.
/// </summary>
public sealed class BillingService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IAgencyRepository agencies,
    IPropertyRepository properties,
    IEntitlementService entitlements,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<AssignPlanRequest> assignValidator,
    IValidator<CheckoutRequest> checkoutValidator,
    IPaymentGateway paymentGateway,
    TimeProvider timeProvider,
    AccountLimits accountLimits) : IBillingService
{
    public async Task<IReadOnlyList<AccountBillingDto>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        var now = timeProvider.GetUtcNow();
        var accounts = await agencies.ListAsync(cancellationToken);
        var active = await subscriptions.ListActiveAsync(now, cancellationToken);
        var usage = await properties.GetUsageByAgencyAsync(now, cancellationToken);
        var free = await plans.GetActiveFreeAsync(cancellationToken);

        return accounts.Select(agency =>
        {
            var mine = usage.GetValueOrDefault(agency.Id, new PropertyUsage(0, 0));
            var listings = active.FirstOrDefault(s => s.AgencyId == agency.Id && s.Kind == PlanKind.Listings);
            var featured = active.FirstOrDefault(s => s.AgencyId == agency.Id && s.Kind == PlanKind.Featured);

            return new AccountBillingDto(
                agency.Id,
                agency.Name,
                agency.IsIndividual,
                Allowances.Listings(agency, listings, free, mine.Total, accountLimits),
                featured is null ? null : Allowances.Featured(featured, mine.Featured));
        }).ToList();
    }

    public async Task<AccountBillingDto> GetMineAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is not (UserRole.AgencyAdmin or UserRole.Supervisor or UserRole.Seller))
        {
            throw new ForbiddenException("Only agency accounts have a plan.");
        }

        var agencyId = currentUser.AgencyId
            ?? throw new ForbiddenException("Your account does not belong to an agency.");
        var agency = await agencies.GetByIdAsync(agencyId, cancellationToken)
            ?? throw new NotFoundException("Agency not found.");

        return new AccountBillingDto(
            agency.Id,
            agency.Name,
            agency.IsIndividual,
            await entitlements.GetListingsAsync(agency, cancellationToken),
            await entitlements.GetFeaturedAsync(agency.Id, cancellationToken));
    }

    public async Task<IReadOnlyList<SubscriptionDto>> ListSubscriptionsAsync(Guid agencyId, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        _ = await agencies.GetByIdAsync(agencyId, cancellationToken) ?? throw new NotFoundException("Agency not found.");

        var now = timeProvider.GetUtcNow();
        return (await subscriptions.ListByAgencyAsync(agencyId, cancellationToken)).Select(s => ToDto(s, now)).ToList();
    }

    public async Task<SubscriptionDto> AssignAsync(Guid agencyId, AssignPlanRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        await assignValidator.ValidateAndThrowAsync(request, cancellationToken);

        var agency = await agencies.GetByIdAsync(agencyId, cancellationToken)
            ?? throw new NotFoundException("Agency not found.");
        var plan = await plans.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");

        EnsureCanSubscribe(agency, plan);

        return await SubscribeAsync(agency, plan, request.AmountPaid ?? plan.Price, request.PaymentReference, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<IReadOnlyList<PlanDto>> ListAvailablePlansAsync(CancellationToken cancellationToken = default)
    {
        var agency = await GetOwnAgencyForPurchaseAsync(cancellationToken);

        return (await plans.ListAsync(null, cancellationToken))
            .Where(p => p.IsActive && !p.IsFree && p.AcceptsAccount(agency.IsIndividual))
            .Select(PlanService.ToDto)
            .ToList();
    }

    public async Task<SubscriptionDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var agency = await GetOwnAgencyForPurchaseAsync(cancellationToken);
        await checkoutValidator.ValidateAndThrowAsync(request, cancellationToken);

        var plan = await plans.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan not found.");
        EnsureCanSubscribe(agency, plan);

        var now = timeProvider.GetUtcNow();
        var card = request.Card.ToCard();

        // La tarjeta vence al terminar el mes indicado.
        if (now >= new DateTimeOffset(card.ExpiryYear, card.ExpiryMonth, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1))
        {
            throw new PaymentDeclinedException("La tarjeta está vencida. Usa otra tarjeta.");
        }

        // Se cobra el precio del plan, nunca un monto que mande el cliente.
        var charge = await paymentGateway.ChargeAsync(new PaymentRequest(plan.Price, plan.Currency, plan.Name, card), cancellationToken);
        if (!charge.Approved)
        {
            throw new PaymentDeclinedException(charge.DeclineReason ?? "El pago fue rechazado. Intenta con otra tarjeta.");
        }

        return await SubscribeAsync(agency, plan, plan.Price, charge.TransactionId, now, cancellationToken);
    }

    /// <summary>El plan debe estar activo, no ser el gratuito y ser para este tipo de cuenta.</summary>
    private static void EnsureCanSubscribe(Agency agency, Plan plan)
    {
        if (!plan.IsActive)
        {
            throw new DomainException("This plan is not active, so it cannot be subscribed to.");
        }

        if (plan.IsFree)
        {
            throw new DomainException("The free plan is not subscribed to: individual accounts have it by default.");
        }

        if (!plan.AcceptsAccount(agency.IsIndividual))
        {
            throw new DomainException("This plan is not available for this kind of account.");
        }
    }

    /// <summary>Crea la contratación: empieza ahora, reemplaza a la vigente del mismo tipo y, si es un paquete de destacados, quita el destacado a los anuncios.</summary>
    private async Task<SubscriptionDto> SubscribeAsync(
        Agency agency,
        Plan plan,
        decimal amountPaid,
        string? paymentReference,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await subscriptions.GetActiveAsync(agency.Id, plan.Kind, now, cancellationToken);
        current?.Cancel(now);
        if (plan.Kind == PlanKind.Featured)
        {
            await ClearFeaturedAsync(agency.Id, now, cancellationToken);
        }

        var subscription = new Subscription(agency.Id, plan, now, amountPaid, paymentReference);
        await subscriptions.AddAsync(subscription, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(subscription, now);
    }

    /// <summary>Solo el administrador de la inmobiliaria (o del espacio del particular) compra para su cuenta.</summary>
    private async Task<Agency> GetOwnAgencyForPurchaseAsync(CancellationToken cancellationToken)
    {
        if (currentUser.Role is not UserRole.AgencyAdmin)
        {
            throw new ForbiddenException("Only the account administrator can buy plans.");
        }

        var agencyId = currentUser.AgencyId
            ?? throw new ForbiddenException("Your account does not belong to an agency.");

        return await agencies.GetByIdAsync(agencyId, cancellationToken)
            ?? throw new NotFoundException("Agency not found.");
    }

    public async Task<SubscriptionDto> CancelAsync(Guid agencyId, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        var subscription = await subscriptions.GetByIdAsync(subscriptionId, cancellationToken);
        if (subscription is null || subscription.AgencyId != agencyId)
        {
            throw new NotFoundException("Subscription not found.");
        }

        var now = timeProvider.GetUtcNow();
        subscription.Cancel(now);
        if (subscription.Kind == PlanKind.Featured)
        {
            await ClearFeaturedAsync(agencyId, now, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(subscription, now);
    }

    private async Task ClearFeaturedAsync(Guid agencyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var property in await properties.ListFeaturedAsync(agencyId, now, cancellationToken))
        {
            property.Unfeature();
        }
    }

    private void EnsurePlatformAdmin()
    {
        if (!currentUser.IsPlatformAdmin)
        {
            throw new ForbiddenException("Only the platform administrator manages subscriptions.");
        }
    }

    internal static SubscriptionDto ToDto(Subscription s, DateTimeOffset now) => new(
        s.Id,
        s.AgencyId,
        s.PlanId,
        s.PlanName,
        s.Kind,
        s.Limit,
        s.Period,
        s.AmountPaid,
        s.Currency,
        s.PaymentReference,
        s.StartsAt,
        s.EndsAt,
        s.CancelledAt,
        s.IsActiveAt(now),
        s.CreatedAt);
}
