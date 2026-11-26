using System.Text.RegularExpressions;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Inquiries;

/// <summary>Lo que escribe el prospecto al pedir informes de una propiedad. No requiere sesión.</summary>
public sealed record CreateInquiryRequest
{
    public string Name { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>La pregunta del prospecto.</summary>
    public string Message { get; init; } = string.Empty;
}

public sealed record AddFollowUpRequest
{
    public FollowUpKind Kind { get; init; } = FollowUpKind.Reply;

    public string Text { get; init; } = string.Empty;
}

public sealed record ChangeInquiryStatusRequest
{
    public InquiryStatus Status { get; init; }
}

public sealed record FollowUpDto(Guid Id, FollowUpKind Kind, string Text, string AuthorName, DateTimeOffset CreatedAt);

/// <summary>Solicitud de informes con los datos del prospecto y su seguimiento (del más antiguo al más reciente).</summary>
public sealed record InquiryDto(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    string Name,
    string Phone,
    string Email,
    string Message,
    InquiryStatus Status,
    IReadOnlyList<FollowUpDto> FollowUps,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <param name="Status">Solo las solicitudes en esta etapa.</param>
/// <param name="Search">Texto que debe aparecer en el nombre, el correo, el teléfono o el título de la propiedad.</param>
public sealed record InquiryListQuery(InquiryStatus? Status = null, string? Search = null, int Page = 1, int PageSize = 10)
{
    public const int MaxPageSize = 100;

    /// <summary>Devuelve una copia con la búsqueda limpia y la paginación acotada a valores seguros.</summary>
    public InquiryListQuery Normalize() => this with
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Page = Math.Max(1, Page),
        PageSize = Math.Clamp(PageSize, 1, MaxPageSize)
    };
}

/// <summary>Límites alineados con las columnas de la base (InquiryConfiguration).</summary>
public static class InquiryLimits
{
    public const int Name = 100;
    public const int Phone = 30;
    public const int Email = 256;
    public const int Text = 2000;
    public const int MinPhoneDigits = 7;
    public const int MaxPhoneDigits = 15;
}

public sealed partial class CreateInquiryRequestValidator : AbstractValidator<CreateInquiryRequest>
{
    public CreateInquiryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(InquiryLimits.Name);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(InquiryLimits.Phone)
            .Must(BeAPhoneNumber).WithMessage("The phone must have between 7 and 15 digits and only contain digits, spaces, + ( ) - or .");

        RuleFor(x => x.Email).NotEmpty().MaximumLength(InquiryLimits.Email).EmailAddress();
        RuleFor(x => x.Message).NotEmpty().MaximumLength(InquiryLimits.Text);
    }

    private static bool BeAPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || !PhoneCharacters().IsMatch(phone))
        {
            return false;
        }

        var digits = phone.Count(char.IsAsciiDigit);
        return digits is >= InquiryLimits.MinPhoneDigits and <= InquiryLimits.MaxPhoneDigits;
    }

    [GeneratedRegex(@"^\+?[0-9()\-\s.]+$")]
    private static partial Regex PhoneCharacters();
}

public sealed class AddFollowUpRequestValidator : AbstractValidator<AddFollowUpRequest>
{
    public AddFollowUpRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(InquiryLimits.Text);
    }
}

public sealed class ChangeInquiryStatusRequestValidator : AbstractValidator<ChangeInquiryStatusRequest>
{
    public ChangeInquiryStatusRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}

internal static class InquiryMapping
{
    public static InquiryDto ToDto(Inquiry inquiry) => new(
        inquiry.Id,
        inquiry.PropertyId,
        inquiry.PropertyTitle,
        inquiry.Name,
        inquiry.Phone,
        inquiry.Email,
        inquiry.Message,
        inquiry.Status,
        inquiry.FollowUps
            .OrderBy(f => f.CreatedAt).ThenBy(f => f.Id)
            .Select(f => new FollowUpDto(f.Id, f.Kind, f.Text, f.AuthorName, f.CreatedAt))
            .ToList(),
        inquiry.CreatedAt,
        inquiry.UpdatedAt);
}