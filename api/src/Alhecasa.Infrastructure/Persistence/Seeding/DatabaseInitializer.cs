using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            if (demo.SampleProperties)
            {
                await EnsureSamplePropertiesAsync(db, logger, agency.Id, cancellationToken);
                await EnsureSampleCoordinatesAsync(db, logger, agency.Id, cancellationToken);
                await EnsureAdvancedDetailsAsync(db, logger, agency.Id, SampleProperties.All, cancellationToken);

                if (!string.IsNullOrWhiteSpace(demo.SampleImagesPath))
                {
                    var folder = Path.GetFullPath(Path.IsPathRooted(demo.SampleImagesPath)
                        ? demo.SampleImagesPath
                        : Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, demo.SampleImagesPath));
                    await EnsureSampleImagesAsync(db, sp.GetRequiredService<IImageStorage>(), logger, agency.Id, folder, cancellationToken);
                }
            }
        }

        if (options.DemoCatalog is { IsConfigured: true } catalog)
        {
            await EnsureDemoCatalogAsync(db, hasher, sp.GetRequiredService<IImageStorage>(), logger, catalog,
                sp.GetRequiredService<IHostEnvironment>().ContentRootPath, cancellationToken);
        }
    }

    /// <summary>
    /// Pone la ubicación a las propiedades de ejemplo que se sembraron antes de que existieran las coordenadas.
    /// Solo toca las que no tienen ninguna; en desarrollo, una propiedad de ejemplo a la que se le quite la ubicación la recupera al reiniciar.
    /// </summary>
    private static async Task EnsureSampleCoordinatesAsync(
        AlhecasaDbContext db,
        ILogger logger,
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var sample in SampleProperties.All.Where(s => s.Latitude is not null))
        {
            var title = sample.Title;
            var property = await db.Properties.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.AgencyId == agencyId && !p.IsDeleted && p.Title == title, cancellationToken);
            if (property is null || property.Latitude is not null || property.Longitude is not null)
            {
                continue;
            }

            property.SetCoordinates(sample.Latitude, sample.Longitude);
            updated++;
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded the location of {Count} sample properties.", updated);
        }
    }

    /// <summary>
    /// Completa estacionamientos, mascotas, amueblado y amenidades de las propiedades de ejemplo que se sembraron antes de
    /// que existieran esos datos. Solo toca las que no tienen ninguno capturado (ver <see cref="Property.CompleteAdvancedDetails"/>).
    /// </summary>
    private static async Task EnsureAdvancedDetailsAsync(
        AlhecasaDbContext db,
        ILogger logger,
        Guid agencyId,
        IReadOnlyList<PropertyData> samples,
        CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var sample in samples)
        {
            if (sample.ParkingSpaces is null && !sample.PetsAllowed && !sample.Furnished && sample.Amenities == Amenity.None)
            {
                continue;
            }

            var title = sample.Title;
            var property = await db.Properties.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.AgencyId == agencyId && !p.IsDeleted && p.Title == title, cancellationToken);
            if (property?.CompleteAdvancedDetails(sample.ParkingSpaces, sample.PetsAllowed, sample.Furnished, sample.Amenities) == true)
            {
                updated++;
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded the advanced details of {Count} sample properties.", updated);
        }
    }

    /// <summary>
    /// Asigna fotos a las propiedades de ejemplo que no tienen ninguna (nunca toca las que ya tienen imágenes).
    /// Cada archivo se valida por su contenido, igual que una subida real.
    /// </summary>
    private static async Task EnsureSampleImagesAsync(
        AlhecasaDbContext db,
        IImageStorage storage,
        ILogger logger,
        Guid agencyId,
        string folder,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Sample images folder {Folder} does not exist; run api/scripts/download-sample-images.ps1.", folder);
            return;
        }

        var assigned = 0;
        for (var index = 0; index < SampleProperties.All.Count; index++)
        {
            var directory = Path.Combine(folder, $"{index + 1:00}");
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var title = SampleProperties.All[index].Title;
            var property = await db.Properties.IgnoreQueryFilters().Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.AgencyId == agencyId && !p.IsDeleted && p.Title == title, cancellationToken);
            if (property is null || property.Images.Count > 0)
            {
                continue;
            }

            await AddImagesFromFolderAsync(property, directory, storage, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            assigned++;
        }

        if (assigned > 0)
        {
            logger.LogInformation("Seeded images for {Count} sample properties.", assigned);
        }
    }

    /// <summary>Guarda las fotos de una carpeta (validadas por su contenido, como una subida real) y las agrega a la propiedad.</summary>
    private static async Task AddImagesFromFolderAsync(
        Property property,
        string directory,
        IImageStorage storage,
        CancellationToken cancellationToken)
    {
            foreach (var path in Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase).Take(Property.MaxImages))
            {
                var info = new FileInfo(path);
                if (info.Length is 0 or > PropertyImageRules.MaxBytes)
                {
                    continue;
                }

                var header = new byte[PropertyImageRules.HeaderLength];
                await using (var probe = info.OpenRead())
                {
                    await probe.ReadAtLeastAsync(header, 1, throwOnEndOfStream: false, cancellationToken);
                }

                if (PropertyImageRules.Detect(header) is not { } format)
                {
                    continue;
                }

                var key = $"{Guid.CreateVersion7():N}.{format.Extension}";
                await using (var content = info.OpenRead())
                {
                    await storage.SaveAsync(key, content, cancellationToken);
                }

                property.AddImage(key, format.ContentType, info.Length);
            }


    }

    /// <summary>
    /// Siembra el catálogo de demostración: inmobiliarias, su personal y sus anuncios con fotos.
    /// Idempotente por correo y por título; lo que ya exista (o se haya borrado) no se vuelve a crear ni a resucitar.
    /// </summary>
    private static async Task EnsureDemoCatalogAsync(
        AlhecasaDbContext db,
        IPasswordHasher hasher,
        IImageStorage storage,
        ILogger logger,
        SeedDemoCatalog catalog,
        string contentRoot,
        CancellationToken cancellationToken)
    {
        var folder = Path.GetFullPath(Path.IsPathRooted(catalog.ImagesPath)
            ? catalog.ImagesPath
            : Path.Combine(contentRoot, catalog.ImagesPath));
        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Demo catalog images folder {Folder} does not exist; listings will have no photos.", folder);
        }

        foreach (var data in DemoCatalog.Agencies)
        {
            var agencyEmail = EmailNormalizer.Normalize(data.Email);
            var agency = await db.Agencies.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => !a.IsDeleted && a.Email == agencyEmail, cancellationToken);
            if (agency is null)
            {
                agency = new Agency(data.Name, agencyEmail, data.Phone);
                db.Agencies.Add(agency);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded demo agency {Agency}.", agency.Name);
            }

            foreach (var user in data.Users)
            {
                await EnsureUserAsync(
                    db,
                    hasher,
                    logger,
                    new SeedUser { FirstName = user.FirstName, LastName = user.LastName, Email = user.Email, Password = catalog.Password },
                    user.Role,
                    agency.Id,
                    cancellationToken);
            }

            await EnsureAdvancedDetailsAsync(db, logger, agency.Id, data.Properties, cancellationToken);

            for (var index = 0; index < data.Properties.Count; index++)
            {
                var property = data.Properties[index];
                var title = property.Title;
                if (await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.AgencyId == agency.Id && p.Title == title, cancellationToken))
                {
                    continue;
                }

                var entity = new Property(agency.Id, property);
                var directory = Path.Combine(folder, $"{data.FirstImageFolder + index:00}");
                if (Directory.Exists(directory))
                {
                    await AddImagesFromFolderAsync(entity, directory, storage, cancellationToken);
                }

                db.Properties.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded demo listing {Title} ({Images} photos).", title, entity.Images.Count);
            }
        }
    }
    /// <summary>Solo siembra si la inmobiliaria no tiene ninguna propiedad (aunque esté borrada), para no duplicar ni resucitar datos.</summary>
    private static async Task EnsureSamplePropertiesAsync(
        AlhecasaDbContext db,
        ILogger logger,
        Guid agencyId,
        CancellationToken cancellationToken)
    {
        if (await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.AgencyId == agencyId, cancellationToken))
        {
            return;
        }

        db.Properties.AddRange(SampleProperties.All.Select(data => new Property(agencyId, data)));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} sample properties.", SampleProperties.All.Count);
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
