using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Properties;

public sealed record PropertyDto(
    Guid Id,
    Guid AgencyId,
    string? AgencyName,
    string Title,
    string? Description,
    PropertyType Type,
    OperationType Operation,
    PropertyStatus Status,
    decimal Price,
    CurrencyCode Currency,
    string? Street,
    string? Neighborhood,
    string City,
    string State,
    string? PostalCode,
    int? Bedrooms,
    int? Bathrooms,
    decimal? ConstructionArea,
    decimal? LandArea,
    IReadOnlyList<Guid> ImageIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Campos que se capturan al crear o editar una propiedad.</summary>
public abstract record PropertyRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public PropertyType Type { get; init; }

    public OperationType Operation { get; init; }

    public PropertyStatus Status { get; init; } = PropertyStatus.Draft;

    public decimal Price { get; init; }

    public CurrencyCode Currency { get; init; } = CurrencyCode.MXN;

    public string? Street { get; init; }

    public string? Neighborhood { get; init; }

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string? PostalCode { get; init; }

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public decimal? ConstructionArea { get; init; }

    public decimal? LandArea { get; init; }

    internal PropertyData ToData() => new(
        Title.Trim(),
        Clean(Description),
        Type,
        Operation,
        Status,
        Price,
        Currency,
        Clean(Street),
        Clean(Neighborhood),
        City.Trim(),
        State.Trim(),
        Clean(PostalCode),
        Bedrooms,
        Bathrooms,
        ConstructionArea,
        LandArea);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreatePropertyRequest : PropertyRequest
{
    /// <summary>
    /// Solo la usa el administrador de plataforma. El personal de una inmobiliaria siempre crea
    /// en la suya; si la envía y es otra, la operación se rechaza.
    /// </summary>
    public Guid? AgencyId { get; init; }
}

/// <summary>Una propiedad no cambia de inmobiliaria: la actualización no recibe <c>AgencyId</c>.</summary>
public sealed record UpdatePropertyRequest : PropertyRequest;

public sealed record PropertyListQuery(string? Search = null, int Page = 1, int PageSize = 10)
{
    public const int MaxPageSize = 100;

    /// <summary>Devuelve una copia con paginación acotada a valores seguros.</summary>
    public PropertyListQuery Normalize() => this with
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize)
    };
}

public abstract class PropertyRequestValidator<T> : AbstractValidator<T>
    where T : PropertyRequest
{
    // Límites alineados con las columnas de la base (PropertyConfiguration).
    public const decimal MaxPrice = 9_999_999_999_999.99m;
    public const decimal MaxArea = 99_999_999.99m;

    protected PropertyRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Operation).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(MaxPrice);
        RuleFor(x => x.Street).MaximumLength(200);
        RuleFor(x => x.Neighborhood).MaximumLength(100);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(10);
        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 50).When(x => x.Bedrooms is not null);
        RuleFor(x => x.Bathrooms).InclusiveBetween(0, 50).When(x => x.Bathrooms is not null);
        RuleFor(x => x.ConstructionArea).InclusiveBetween(0, MaxArea).When(x => x.ConstructionArea is not null);
        RuleFor(x => x.LandArea).InclusiveBetween(0, MaxArea).When(x => x.LandArea is not null);
    }
}

public sealed class CreatePropertyRequestValidator : PropertyRequestValidator<CreatePropertyRequest>;

public sealed class UpdatePropertyRequestValidator : PropertyRequestValidator<UpdatePropertyRequest>;
