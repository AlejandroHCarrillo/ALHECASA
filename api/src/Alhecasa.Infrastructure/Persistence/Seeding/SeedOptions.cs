namespace Alhecasa.Infrastructure.Persistence.Seeding;

/// <summary>
/// Datos iniciales opcionales. Las contraseñas se leen de user-secrets o variables de entorno,
/// nunca de appsettings.json versionado.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public SeedUser? PlatformAdmin { get; init; }

    public SeedAgency? DemoAgency { get; init; }
}

public sealed class SeedUser
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}

public sealed class SeedAgency
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public SeedUser? Admin { get; init; }
}
