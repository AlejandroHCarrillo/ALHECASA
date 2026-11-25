using Alhecasa.Domain.Enums;
using Alhecasa.Application.Users;
using FluentValidation;

namespace Alhecasa.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    Guid? AgencyId,
    string? AgencyName,
    /// <summary>Cuenta gratuita de un particular (no es una inmobiliaria con personal).</summary>
    bool IsIndividual = false);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

/// <summary>Alta pública de una cuenta gratuita de particular.</summary>
public sealed record RegisterRequest(string FirstName, string LastName, string Email, string Password);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).ValidName();
        RuleFor(x => x.LastName).ValidName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).StrongPassword();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
