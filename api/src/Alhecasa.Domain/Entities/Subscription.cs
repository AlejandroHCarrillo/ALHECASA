using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

/// <summary>
/// Una contratación de un plan por una cuenta. Copia lo contratado (tipo, tope, nombre y precio) para que editar o eliminar
/// el plan después no cambie lo que la cuenta ya pagó. Dura un periodo desde que empieza; por ahora no hay cobro en línea:
/// el administrador de plataforma la registra al recibir el pago.
/// </summary>
public class Subscription : BaseEntity
{
    private Subscription()
    {
    }

    public Subscription(Guid agencyId, Plan plan, DateTimeOffset now, decimal amountPaid, string? paymentReference)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (agencyId == Guid.Empty)
        {
            throw new DomainException("A subscription must belong to an account.");
        }

        if (plan.IsFree || plan.Period is not { } period)
        {
            throw new DomainException("A free plan is not subscribed to: individual accounts have it by default.");
        }

        if (amountPaid is < 0 or > Plan.MaxPrice)
        {
            throw new DomainException("The amount paid is not valid.");
        }

        AgencyId = agencyId;
        PlanId = plan.Id;
        PlanName = plan.Name;
        Kind = plan.Kind;
        Limit = plan.Limit;
        Period = period;
        AmountPaid = amountPaid;
        Currency = plan.Currency;
        PaymentReference = string.IsNullOrWhiteSpace(paymentReference) ? null : paymentReference.Trim();
        StartsAt = now;
        EndsAt = BillingPeriods.EndOf(now, period);
    }

    public Guid AgencyId { get; private set; }

    public Guid PlanId { get; private set; }

    /// <summary>Nombre del plan al contratar.</summary>
    public string PlanName { get; private set; } = string.Empty;

    public PlanKind Kind { get; private set; }

    /// <summary>Tope contratado. Nulo = ilimitado.</summary>
    public int? Limit { get; private set; }

    public BillingPeriod Period { get; private set; }

    public decimal AmountPaid { get; private set; }

    public CurrencyCode Currency { get; private set; }

    /// <summary>Folio, referencia de transferencia o nota con la que el administrador registró el pago.</summary>
    public string? PaymentReference { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>Si se canceló o se reemplazó por otra contratación del mismo tipo antes de vencer.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => CancelledAt is null && StartsAt <= now && now < EndsAt;

    /// <summary>Termina la contratación ahora. No hace nada si ya estaba cancelada.</summary>
    public void Cancel(DateTimeOffset now) => CancelledAt ??= now;
}

/// <summary>Cálculo de cuándo termina un periodo de contratación.</summary>
public static class BillingPeriods
{
    public static DateTimeOffset EndOf(DateTimeOffset start, BillingPeriod period) => period switch
    {
        BillingPeriod.Day => start.AddDays(1),
        BillingPeriod.Week => start.AddDays(7),
        BillingPeriod.Month => start.AddMonths(1),
        BillingPeriod.Year => start.AddYears(1),
        _ => throw new DomainException("The billing period is not valid.")
    };
}
