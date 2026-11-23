using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alhecasa.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    /// <summary>Aplica las migraciones pendientes.</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AlhecasaDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>Crea el administrador de plataforma y la inmobiliaria demo si están configurados y no existen.</summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var options = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        var db = sp.GetRequiredService<AlhecasaDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        if (options.PlatformAdmin is { IsConfigured: true } admin)
        {
            await EnsureUserAsync(db, hasher, logger, admin, UserRole.PlatformAdmin, null, cancellationToken);
        }

        if (options.DemoAgency is { } demo && !string.IsNullOrWhiteSpace(demo.Name) && !string.IsNullOrWhiteSpace(demo.Email))
        {
            var agencyEmail = EmailNormalizer.Normalize(demo.Email);
            var agency = await db.Agencies.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => !a.IsDeleted && a.Email == agencyEmail, cancellationToken);

            if (agency is null)
            {
                agency = new Agency(demo.Name.Trim(), agencyEmail, demo.Phone);
                db.Agencies.Add(agency);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded demo agency {Agency}.", agency.Name);
            }

            if (demo.Admin is { IsConfigured: true } agencyAdmin)
            {
                await EnsureUserAsync(db, hasher, logger, agencyAdmin, UserRole.AgencyAdmin, agency.Id, cancellationToken);
            }
        }
    }

    private static async Task EnsureUserAsync(
        AlhecasaDbContext db,
        IPasswordHasher hasher,
        ILogger logger,
        SeedUser seed,
        UserRole role,
        Guid? agencyId,
        CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(seed.Email);

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => !u.IsDeleted && u.Email == email, cancellationToken))
        {
            return;
        }

        db.Users.Add(new User(
            string.IsNullOrWhiteSpace(seed.FirstName) ? role.ToString() : seed.FirstName.Trim(),
            string.IsNullOrWhiteSpace(seed.LastName) ? "Seed" : seed.LastName.Trim(),
            email,
            hasher.Hash(seed.Password),
            role,
            agencyId));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Role} user {Email}.", role, email);
    }
}
