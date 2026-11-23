using Alhecasa.Application.Agencies;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Alhecasa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAgencyService, AgencyService>();

        return services;
    }
}
