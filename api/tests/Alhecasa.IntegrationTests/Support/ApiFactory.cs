using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Alhecasa.IntegrationTests.Support;

/// <summary>
/// SQLite no puede ordenar ni comparar <see cref="DateTimeOffset"/> (lo guarda como texto) ni decimal. Se guardan como entero
/// y como double solo en las pruebas, para poder ejecutar las mismas consultas que en SQL Server (por ejemplo ORDER BY CreatedAt
/// o Price >= x).
/// </summary>
internal sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        var converter = new DateTimeOffsetToBinaryConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
        {
            property.SetValueConverter(converter);
        }

        var decimalConverter = new DecimalToDoubleConverter();
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetValueConverter(property.ClrType == typeof(decimal?)
                ? new ValueConverter<decimal?, double?>(v => v == null ? null : (double)v.Value, v => v == null ? null : (decimal)v.Value)
                : decimalConverter);
        }
    }
}

/// <summary>Guarda los decimal como double solo en las pruebas (ver <see cref="SqliteModelCustomizer"/>).</summary>
internal sealed class DecimalToDoubleConverter() : ValueConverter<decimal, double>(v => (double)v, v => (decimal)v);

/// <summary>
/// Levanta la API real en memoria con una base SQLite efímera (una por instancia).
/// Se usa SQLite y no SQL Server para que las pruebas no dependan de LocalDB ni de una instancia instalada;
/// el esquema se crea desde el modelo de EF (EnsureCreated), no con las migraciones de SQL Server.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string UserPassword = "Correct-Horse-9";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private string? _passwordHash;

    public ApiFactory()
    {
        // La base en memoria vive mientras la conexión esté abierta.
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789-abcdef");
        builder.UseSetting("Payments:AllowSimulated", "true");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");

        // Las pruebas comparten la misma IP: se sube el tope del formulario público para que no estorbe entre sí.
        // PublicInquiriesRateLimitTests lo vuelve a bajar para comprobar el límite real.
        builder.UseSetting("RateLimiting:Inquiries:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Register:PermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            // Sustituye el proveedor SQL Server por SQLite. La Fase 0 (F0-03) vuelve esto innecesario
            // al seleccionar el proveedor por configuración.
            // AddDbContext registra DbContextOptions<T> y una IDbContextOptionsConfiguration<T> (tipo interno de EF)
            // con la configuración de SQL Server; ambas se quitan por tipo genérico para no depender de API interna.
            var previous = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AlhecasaDbContext>)
                    || (d.ServiceType.IsGenericType
                        && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
                        && d.ServiceType.GenericTypeArguments.Contains(typeof(AlhecasaDbContext))))
                .ToList();
            foreach (var descriptor in previous)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AlhecasaDbContext>(options => options
                .UseSqlite(_connection)
                .ReplaceService<IModelCustomizer, SqliteModelCustomizer>());

            services.RemoveAll<IImageStorage>();
            services.AddSingleton(Storage);
            services.AddSingleton<IImageStorage>(sp => sp.GetRequiredService<InMemoryImageStorage>());
        });
    }

    public InMemoryImageStorage Storage { get; } = new();

    /// <summary>Agrega imágenes JPEG a una propiedad (base y almacenamiento), como si ya se hubieran subido.</summary>
    public async Task<IReadOnlyList<PropertyImage>> AddImagesAsync(Guid propertyId, int count = 1)
    {
        var added = new List<PropertyImage>();
        await WithDbAsync(async db =>
        {
            var property = await db.Properties.IgnoreQueryFilters().Include(p => p.Images).SingleAsync(p => p.Id == propertyId);
            for (var i = 0; i < count; i++)
            {
                var key = $"{Guid.CreateVersion7():N}.jpg";
                await Storage.SaveAsync(key, new MemoryStream(JpegBytes));
                added.Add(property.AddImage(key, "image/jpeg", JpegBytes.Length));
            }

            await db.SaveChangesAsync();
        });
        return added;
    }

    public static byte[] JpegBytes { get; } = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3];

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AlhecasaDbContext>().Database.EnsureCreated();

        return host;
    }

    /// <summary>Ejecuta código contra la base de datos fuera de cualquier solicitud HTTP.</summary>
    public async Task WithDbAsync(Func<AlhecasaDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlhecasaDbContext>();
        await action(db);
    }

    public async Task<Agency> AddAgencyAsync(string name = "Agency")
    {
        var agency = new Agency(name, $"{Guid.NewGuid():N}@agency.test", null);
        await WithDbAsync(async db =>
        {
            db.Agencies.Add(agency);
            await db.SaveChangesAsync();
        });
        return agency;
    }

    public async Task<User> AddUserAsync(UserRole role, Guid? agencyId, string? email = null)
    {
        var user = new User(
            "Test",
            role.ToString(),
            email ?? $"{Guid.NewGuid():N}@test.local",
            PasswordHash(),
            role,
            agencyId);

        await WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        return user;
    }

    /// <summary>Agrega una propiedad con todos sus datos (para probar filtros por precio, recámaras, amenidades…).</summary>
    public async Task<Property> AddPropertyAsync(Guid agencyId, PropertyData data)
    {
        var property = new Property(agencyId, data);
        await WithDbAsync(async db =>
        {
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        });
        return property;
    }

    public async Task<Property> AddPropertyAsync(
        Guid agencyId,
        string title = "Casa de prueba",
        string city = "Ciudad de México",
        PropertyStatus status = PropertyStatus.Draft,
        string? street = null,
        PropertyType type = PropertyType.House,
        OperationType operation = OperationType.Sale,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        var property = new Property(agencyId, new PropertyData(
            title,
            null,
            type,
            operation,
            status,
            1_000_000m,
            CurrencyCode.MXN,
            street,
            null,
            city,
            "CDMX",
            null,
            null,
            null,
            null,
            null,
            latitude,
            longitude));

        await WithDbAsync(async db =>
        {
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        });
        return property;
    }

    /// <summary>Agrega una solicitud de informes directamente en la base, como si un visitante ya la hubiera mandado.</summary>
    public async Task<Inquiry> AddInquiryAsync(Guid agencyId, Guid propertyId, string name = "Ana López", string propertyTitle = "Casa de prueba", string message = "¿Sigue disponible?")
    {
        var inquiry = new Inquiry(propertyId, agencyId, propertyTitle, name, "5512345678", $"{Guid.NewGuid():N}@prospecto.test", message);
        await WithDbAsync(async db =>
        {
            db.Inquiries.Add(inquiry);
            await db.SaveChangesAsync();
        });
        return inquiry;
    }
    /// <summary>Modifica un usuario existente directamente en la base (simula un cambio hecho por otro administrador).</summary>
    public Task ModifyUserAsync(Guid userId, Action<User> change) =>
        WithDbAsync(async db =>
        {
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == userId);
            change(user);
            await db.SaveChangesAsync();
        });

    public Task ModifyAgencyAsync(Guid agencyId, Action<Agency> change) =>
        WithDbAsync(async db =>
        {
            var agency = await db.Agencies.IgnoreQueryFilters().SingleAsync(a => a.Id == agencyId);
            change(agency);
            await db.SaveChangesAsync();
        });

    /// <summary>Emite un JWT válido para el usuario sin pasar por el login (que tiene límite de intentos).</summary>
    public string CreateToken(User user) =>
        Services.GetRequiredService<IJwtTokenGenerator>().Generate(user).Value;

    public HttpClient CreateClientFor(User user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(user));
        return client;
    }

    // El hash de PBKDF2 es lento a propósito: se calcula una sola vez y se reutiliza.
    private string PasswordHash() =>
        _passwordHash ??= Services.GetRequiredService<IPasswordHasher>().Hash(UserPassword);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
