using Alhecasa.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Alhecasa.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IRegistrationService registrationService) : ControllerBase
{
    /// <summary>Inicia sesión y devuelve un JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request, cancellationToken));

    /// <summary>
    /// Crea una cuenta gratuita de particular para publicar anuncios y deja la sesión iniciada.
    /// El rol y el espacio de la cuenta los fija el servidor; el cliente no puede elegirlos.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await registrationService.RegisterAsync(request, cancellationToken));

    /// <summary>Devuelve el usuario autenticado.</summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetCurrentUserAsync(cancellationToken));
}
