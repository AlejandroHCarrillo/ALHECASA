using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Billing;

public sealed record PlanDto(
    Guid Id,
    string Name,
    string? Description,
    PlanKind Kind,
    PlanAudience Audience,
    int? Limit,
    BillingPeriod? Period,
    decimal Price,
    CurrencyCode Currency,
    bool IsActive,
    bool IsFree,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Campos que se capturan al crear o editar un plan.</summary>
public sealed record SavePlanRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public PlanKind Kind { get; init; } = PlanKind.Listings;

    public PlanAudience Audience { get; init; } = PlanAudience.Company;

    /// <summary>Publicaciones o destacados a la vez. Nulo = ilimitado.</summary>
    public int? Limit { get; init; }

    public BillingPeriod? Period { get; init; }

    public decimal Price { get; init; }

    public CurrencyCode Currency { get; init; } = CurrencyCode.MXN;

    public bool IsActive { get; init; } = true;

    internal PlanData ToData() => new(Name, Description, Kind, Audience, Limit, Period, Price, Currency, IsActive);
}

public sealed class SavePlanRequestValidator : AbstractValidator<SavePlanRequest>
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public SavePlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(DescriptionMaxLength);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Audience).IsInEnum();
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Period).IsInEnum().When(x => x.Period is not null);
        RuleFor(x => x.Limit).InclusiveBetween(1, Plan.MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x.Price).InclusiveBetween(0m, Plan.MaxPrice);
    }
}

/// <summary>Registra que una cuenta contrató un plan (por ahora el pago se recibe fuera del sistema).</summary>
/// <param name="AmountPaid">Lo que pagó. Si se omite, el precio del plan.</param>
/// <param name="PaymentReference">Folio, referencia de transferencia o nota del pago.</param>
public sealed record AssignPlanRequest(Guid PlanId, decimal? AmountPaid = null, string? PaymentReference = null);

public sealed class AssignPlanRequestValidator : AbstractValidator<AssignPlanRequest>
{
    public AssignPlanRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.AmountPaid).InclusiveBetween(0m, Plan.MaxPrice).When(x => x.AmountPaid is not null);
        RuleFor(x => x.PaymentReference).MaximumLength(200);
    }
}

public sealed record SubscriptionDto(
    Guid Id,
    Guid AgencyId,
    Guid PlanId,
    string PlanName,
    PlanKind Kind,
    int? Limit,
    BillingPeriod Period,
    decimal AmountPaid,
    CurrencyCode Currency,
    string? PaymentReference,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CancelledAt,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Publicaciones que permite la cuenta y cuántas usa.</summary>
/// <param name="PlanName">Nombre del plan vigente. Nulo si una inmobiliaria no tiene ninguno contratado (sin tope).</param>
/// <param name="Limit">Nulo = ilimitado.</param>
/// <param name="Used">Propiedades que tiene la cuenta (borradores incluidos, sin las eliminadas).</param>
/// <param name="EndsAt">Cuándo vence el plan contratado. Nulo en el plan gratuito y sin plan.</param>
/// <param name="IsFree">Si es el plan gratuito de los particulares.</param>
public sealed record ListingsAllowanceDto(string? PlanName, int? Limit, int Used, DateTimeOffset? EndsAt, bool IsFree);

/// <summary>Paquete de destacados vigente: cuántos anuncios pueden estar destacados a la vez y cuántos lo están.</summary>
public sealed record FeaturedAllowanceDto(string PlanName, int? Limit, int Used, DateTimeOffset EndsAt);

/// <summary>Lo que tiene contratado una cuenta y lo que usa.</summary>
/// <param name="Featured">Nulo si la cuenta no tiene un paquete de destacados vigente.</param>
public sealed record AccountBillingDto(
    Guid AgencyId,
    string AgencyName,
    bool IsIndividual,
    ListingsAllowanceDto Listings,
    FeaturedAllowanceDto? Featured);

/// <summary>
/// Compra de un plan por la propia cuenta. El precio sale del plan, nunca del cliente: lo único que se elige es el plan.
/// </summary>
public sealed record CheckoutRequest(Guid PlanId, PaymentCardRequest Card);

/// <summary>Datos de la tarjeta. Sensibles: no se guardan y <see cref="ToString"/> los oculta.</summary>
/// <param name="ExpiryYear">Año de cuatro cifras.</param>
public sealed record PaymentCardRequest(string Number, string HolderName, int ExpiryMonth, int ExpiryYear, string Cvv)
{
    public override string ToString() => "PaymentCardRequest { **** }";

    internal PaymentCard ToCard() => new(CardNumbers.Digits(Number), HolderName.Trim(), ExpiryMonth, ExpiryYear, Cvv);
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public const int HolderNameMaxLength = 100;

    public CheckoutRequestValidator()
    {
        RuleFor(x => x.PlanId).NotEmpty();
        RuleFor(x => x.Card).NotNull().SetValidator(new PaymentCardRequestValidator());
    }
}

public sealed class PaymentCardRequestValidator : AbstractValidator<PaymentCardRequest>
{
    public PaymentCardRequestValidator()
    {
        // Los mensajes no incluyen el valor capturado: un número de tarjeta no debe viajar en una respuesta de error.
        RuleFor(x => x.Number)
            .NotEmpty()
            .Must(n => CardNumbers.IsValid(n)).WithMessage("The card number is not valid.");
        RuleFor(x => x.HolderName).NotEmpty().MaximumLength(CheckoutRequestValidator.HolderNameMaxLength);
        RuleFor(x => x.ExpiryMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ExpiryYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Cvv)
            .NotEmpty()
            .Must(c => c is { Length: 3 or 4 } && c.All(char.IsAsciiDigit)).WithMessage("The security code must have 3 or 4 digits.");
    }
}

/// <summary>Número de tarjeta: acepta espacios y guiones al capturar y verifica la longitud y el dígito de control (Luhn).</summary>
internal static class CardNumbers
{
    public static string Digits(string number) => new(number.Where(char.IsAsciiDigit).ToArray());

    public static bool IsValid(string? number)
    {
        if (string.IsNullOrWhiteSpace(number) || number.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-')))
        {
            return false;
        }

        var digits = Digits(number);
        if (digits.Length is < 12 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }
}
