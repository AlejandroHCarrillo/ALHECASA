namespace Alhecasa.Application.Abstractions;

/// <summary>
/// Almacenamiento de los archivos de imagen. La aplicación solo conoce claves (<c>StorageKey</c>);
/// dónde viven los archivos (disco, nube) es un detalle de infraestructura.
/// </summary>
public interface IImageStorage
{
    /// <summary>Guarda el contenido bajo la clave indicada.</summary>
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Abre el archivo para lectura, o devuelve null si no existe.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Borra el archivo. Si no existe, no hace nada.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
