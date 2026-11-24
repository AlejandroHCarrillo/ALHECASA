using Alhecasa.Domain.Common;

namespace Alhecasa.Domain.Entities;

/// <summary>
/// Imagen de una propiedad. El archivo vive en el almacenamiento de imágenes; aquí solo se guarda su clave.
/// Se crea y se quita únicamente desde <see cref="Property"/>, que garantiza el límite de imágenes.
/// </summary>
public class PropertyImage : BaseEntity
{
    private PropertyImage()
    {
    }

    internal PropertyImage(Guid propertyId, Guid agencyId, string storageKey, string contentType, long sizeBytes, int sortOrder)
    {
        PropertyId = propertyId;
        AgencyId = agencyId;
        StorageKey = storageKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        SortOrder = sortOrder;
    }

    public Guid PropertyId { get; private set; }

    /// <summary>Inmobiliaria dueña (la misma de la propiedad): permite aislar por tenant sin cruzar tablas.</summary>
    public Guid AgencyId { get; private set; }

    /// <summary>Nombre del archivo en el almacenamiento. Lo genera el sistema; nunca viene del cliente.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Posición en la galería. La primera es la portada.</summary>
    public int SortOrder { get; private set; }
}
