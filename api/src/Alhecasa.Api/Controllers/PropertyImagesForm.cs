namespace Alhecasa.Api.Controllers;

/// <summary>Cuerpo multipart para cambiar la galería de una propiedad.</summary>
public sealed class PropertyImagesForm
{
    /// <summary>Imágenes nuevas, en el orden en que se agregan al final de la galería.</summary>
    public IFormFileCollection? Files { get; init; }

    /// <summary>Identificadores de las imágenes actuales que se quitan.</summary>
    public List<Guid>? RemoveImageIds { get; init; }
}
