using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Alhecasa.IntegrationTests.Support;

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
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");

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

            services.AddDbContext<AlhecasaDbContext>(options => options.UseSqlite(_connection));
        });
    }

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
