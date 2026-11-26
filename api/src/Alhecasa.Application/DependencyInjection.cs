using Alhecasa.Application.Agencies;
using Alhecasa.Application.Auth;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Application.Inquiries;
using Alhecasa.Application.Properties;
using Alhecasa.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Alhecasa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, AccountLimits? accountLimits = null)
    {
        services.AddSingleton(accountLimits ?? new AccountLimits());
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAgencyService, AgencyService>();
        services.AddScoped<IPublicAgencyService, PublicAgencyService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IPublicPropertyService, PublicPropertyService>();
        services.AddScoped<IPublicInquiryService, PublicInquiryService>();
        services.AddScoped<IInquiryService, InquiryService>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
