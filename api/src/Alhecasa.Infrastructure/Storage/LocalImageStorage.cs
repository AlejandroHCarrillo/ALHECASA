using System.Text.RegularExpressions;
using Alhecasa.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Alhecasa.Infrastructure.Storage;

/// <summary>Opciones del almacenamiento de imágenes.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Carpeta donde se guardan las imágenes. Si es relativa, cuelga de la carpeta de la aplicación.</summary>
    public string ImagesPath { get; init; } = "App_Data/images";
}

/// <summary>
/// Guarda las imágenes como archivos en una carpeta fuera de la raíz web: nada se sirve directo desde el disco,
/// todo pasa por la API. Solo acepta las claves que genera el sistema (32 hex + extensión), así una clave
/// manipulada no puede salir de la carpeta.
/// </summary>
internal sealed partial class LocalImageStorage : IImageStorage
{
    private readonly string _root;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(IOptions<StorageOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment environment, ILogger<LocalImageStorage> logger)
    {
        _logger = logger;
        var path = options.Value.ImagesPath;
        _root = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default)
    {
        await using var file = new FileStream(PathFor(key), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            File.Delete(PathFor(key));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // El archivo queda huérfano; no es motivo para fallar una operación que ya se guardó en la base.
            _logger.LogWarning(ex, "Could not delete image file {Key}.", key);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        if (!ValidKey().IsMatch(key))
        {
            throw new ArgumentException("Invalid image key.", nameof(key));
        }

        return Path.Combine(_root, key);
    }

    [GeneratedRegex("^[0-9a-f]{32}\\.(jpg|png|webp)$")]
    private static partial Regex ValidKey();
}
