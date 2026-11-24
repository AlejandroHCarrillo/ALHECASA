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

    /// <summary>Si es verdadero y la inmobiliaria aún no tiene propiedades, crea un inventario de ejemplo (solo para desarrollo).</summary>
    public bool SampleProperties { get; init; }

    /// <summary>
    /// Carpeta con las fotos de las propiedades de ejemplo: una subcarpeta por propiedad (01, 02… en el orden de
    /// <c>SampleProperties</c>). Las propiedades de ejemplo que no tengan imágenes reciben las de su subcarpeta
    /// (máximo 10). Si es relativa, cuelga de la carpeta de la aplicación. Solo para desarrollo;
    /// <c>api/scripts/download-sample-images.ps1</c> la llena.
    /// </summary>
    public string? SampleImagesPath { get; init; }
}
