using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

/// <summary>Datos editables de un plan.</summary>
/// <param name="Limit">Publicaciones (plan de publicaciones) o anuncios destacados a la vez (paquete de destacados). Nulo = ilimitado.</param>
/// <param name="Period">Duración de una contratación. Obligatoria en los planes de pago; un plan gratuito no vence.</param>
public sealed record PlanData(
    string Name,
    string? Description,
    PlanKind Kind,
    PlanAudience Audience,
    int? Limit,
    BillingPeriod? Period,
    decimal Price,
    CurrencyCode Currency,
    bool IsActive = true);

/// <summary>
/// Un plan del catálogo de la plataforma para monetizar el portal. No pertenece a una inmobiliaria: lo administra el administrador de plataforma.
/// El plan gratuito (precio 0) es el que tienen por omisión los particulares: solo puede ser de publicaciones, para particulares,
/// con tope y sin vencimiento. Los demás son de pago y duran un periodo.
/// </summary>
public class Plan : BaseEntity
{
    /// <summary>Tope más alto aceptado (más allá de eso se usa «ilimitado»).</summary>
    public const int MaxLimit = 100_000;

    public const decimal MaxPrice = 9_999_999_999.99m;

    private Plan()
    {
    }

    public Plan(PlanData data) => Apply(data);

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PlanKind Kind { get; private set; }

    public PlanAudience Audience { get; private set; }

    /// <summary>Nulo = ilimitado.</summary>
    public int? Limit { get; private set; }

    /// <summary>Nulo solo en el plan gratuito, que no vence.</summary>
    public BillingPeriod? Period { get; private set; }

    public decimal Price { get; private set; }

    public CurrencyCode Currency { get; private set; }

    /// <summary>Un plan inactivo no se puede contratar; las contrataciones que ya existen siguen vigentes.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>El plan gratuito es el que tienen por omisión los particulares.</summary>
    public bool IsFree => Price == 0;

    public void Update(PlanData data) => Apply(data);

    private void Apply(PlanData data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw new DomainException("The plan name is required.");
        }

        if (!Enum.IsDefined(data.Kind) || !Enum.IsDefined(data.Audience) || !Enum.IsDefined(data.Currency))
        {
            throw new DomainException("The plan kind, audience or currency is not valid.");
        }

        if (data.Limit is < 1 or > MaxLimit)
        {
            throw new DomainException($"The limit must be between 1 and {MaxLimit}, or unlimited.");
        }

        if (data.Price is < 0 or > MaxPrice)
        {
            throw new DomainException("The price is not valid.");
        }

        if (data.Period is { } period && !Enum.IsDefined(period))
        {
            throw new DomainException("The billing period is not valid.");
        }

        if (data.Price == 0)
        {
            if (data.Kind != PlanKind.Listings)
            {
                throw new DomainException("Only a listings plan can be free.");
            }

            if (data.Audience != PlanAudience.Individual)
            {
                throw new DomainException("A free plan is only for individual accounts.");
            }

            if (data.Limit is null)
            {
                throw new DomainException("A free plan needs a limit.");
            }

            if (!data.IsActive)
            {
                throw new DomainException("The free plan must stay active.");
            }
        }
        else if (data.Period is null)
        {
            throw new DomainException("A paid plan needs a billing period.");
        }

        Name = data.Name.Trim();
        Description = string.IsNullOrWhiteSpace(data.Description) ? null : data.Description.Trim();
        Kind = data.Kind;
        Audience = data.Audience;
        Limit = data.Limit;
        Period = data.Price == 0 ? null : data.Period;
        Price = data.Price;
        Currency = data.Currency;
        IsActive = data.IsActive;
    }

    /// <summary>Si una cuenta de este tipo puede contratar el plan.</summary>
    public bool AcceptsAccount(bool isIndividual) => Audience switch
    {
        PlanAudience.Individual => isIndividual,
        PlanAudience.Company => !isIndividual,
        _ => true
    };
}
