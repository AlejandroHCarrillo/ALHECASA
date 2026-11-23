using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using FluentValidation;

namespace Alhecasa.Application.Auth;

public sealed class AuthService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ICurrentUser currentUser,
    IValidator<LoginRequest> validator,
    TimeProvider timeProvider) : IAuthService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var email = EmailNormalizer.Normalize(request.Email);
        var user = await users.GetByEmailForAuthenticationAsync(email, cancellationToken);

        var passwordOk = false;
        if (user is null)
        {
            passwordHasher.VerifyDummy(request.Password);
        }
        else
        {
            passwordOk = passwordHasher.Verify(request.Password, user.PasswordHash);
        }

        if (user is null || !passwordOk || !user.IsActive || user.Agency is { IsActive: false })
        {
            throw new AuthenticationFailedException(InvalidCredentialsMessage);
        }

        user.RegisterLogin(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokenGenerator.Generate(user);
        return new LoginResponse(token.Value, token.ExpiresAt, ToDto(user));
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException("Not authenticated.");

        var user = await users.GetByIdAsync(userId, cancellationToken);

        // GetByIdAsync aplica el filtro por inmobiliaria; un usuario siempre se ve a sí mismo.
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException("Not authenticated.");
        }

        return ToDto(user);
    }

    private static CurrentUserDto ToDto(User user) =>
        new(user.Id, user.FullName, user.Email, user.Role, user.AgencyId, user.Agency?.Name);
}
