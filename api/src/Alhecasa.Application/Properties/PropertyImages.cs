namespace Alhecasa.Application.Properties;

/// <summary>Un archivo recibido para agregar a la galería de una propiedad.</summary>
public sealed record ImageUpload(Stream Content, long Length);

/// <summary>Formato de imagen aceptado.</summary>
public sealed record ImageFormat(string ContentType, string Extension);

/// <summary>Reglas de las imágenes de una propiedad.</summary>
public static class PropertyImageRules
{
    /// <summary>Tamaño máximo de cada archivo (5 MB).</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Bytes iniciales que bastan para reconocer el formato.</summary>
    public const int HeaderLength = 12;

    /// <summary>
    /// Reconoce JPEG, PNG y WebP por el contenido del archivo (su firma), no por el nombre ni por el tipo
    /// que declara el cliente, que se pueden falsificar. Devuelve null si no es un formato aceptado.
    /// </summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new ImageFormat("image/jpeg", "jpg");
        }

        if (header.Length >= 8 && header[..8].SequenceEqual<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return new ImageFormat("image/png", "png");
        }

        // WebP: "RIFF" + 4 bytes de tamaño + "WEBP".
        if (header.Length >= 12
            && header[..4].SequenceEqual<byte>("RIFF"u8)
            && header[8..12].SequenceEqual<byte>("WEBP"u8))
        {
            return new ImageFormat("image/webp", "webp");
        }

        return null;
    }
}
