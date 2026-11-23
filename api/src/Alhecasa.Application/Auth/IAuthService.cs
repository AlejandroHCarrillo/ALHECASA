namespace Alhecasa.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
