using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Auth;

public interface IRegistrationService
{
    /// <summary>Crea la cuenta gratuita de un particular y deja la sesión iniciada.</summary>
    Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registro público de un particular que quiere publicar su anuncio sin costo.
/// Se le crea un espacio propio (una inmobiliaria de tipo <see cref="AgencyKind.Individual"/>) y un usuario
/// <see cref="UserRole.AgencyAdmin"/> en él: así sus propiedades y solicitudes quedan aisladas con las mismas
/// reglas que las de una inmobiliaria, sin un modelo paralelo. No puede dar de alta personal ni excede el tope de propiedades.
/// El rol <see cref="UserRole.PlatformAdmin"/> jamás se puede obtener por aquí: el rol lo fija el servidor.
/// </summary>
public sealed class RegistrationService(
    IUserRepository users,
    IAgencyRepository agencies,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    IValidator<RegisterRequest> validator,
    TimeProvider timeProvider) : IRegistrationService
{
    private const int AgencyNameMaxLength = 200;

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var email = EmailNormalizer.Normalize(request.Email);
        if (await users.EmailExistsAsync(email, null, cancellationToken))
        {
            throw new ConflictException("The email is already registered.");
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var name = $"{firstName} {lastName}".Trim();

        // Un solo SaveChanges: la cuenta nace completa (espacio + usuario) o no nace.
        var agency = Agency.CreateIndividual(name.Length <= AgencyNameMaxLength ? name : name[..AgencyNameMaxLength], email);
        var user = new User(firstName, lastName, email, passwordHasher.Hash(request.Password), UserRole.AgencyAdmin, agency.Id);
        user.RegisterLogin(timeProvider.GetUtcNow());

        await agencies.AddAsync(agency, cancellationToken);
        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokenGenerator.Generate(user);
        return new LoginResponse(
            token.Value,
            token.ExpiresAt,
            new CurrentUserDto(user.Id, user.FullName, user.Email, user.Role, agency.Id, agency.Name, IsIndividual: true));
    }
}