using Alhecasa.Application.Abstractions;
using Alhecasa.Infrastructure.Identity;
using Alhecasa.Infrastructure.Persistence;
using Alhecasa.Infrastructure.Persistence.Repositories;
using Alhecasa.Infrastructure.Persistence.Seeding;
using Alhecasa.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Alhecasa.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it in appsettings or user-secrets.");

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AlhecasaDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AlhecasaDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure();
            }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AlhecasaDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAgencyRepository, AgencyRepository>();
        services.AddScoped<IPropertyRepository, PropertyRepository>();
        services.AddScoped<IInquiryRepository, InquiryRepository>();

        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IImageStorage, LocalImageStorage>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        return services;
    }
}
