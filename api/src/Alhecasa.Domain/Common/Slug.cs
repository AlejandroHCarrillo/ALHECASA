using System.Globalization;
using System.Text;

namespace Alhecasa.Domain.Common;

/// <summary>
/// Identificadores de texto para direcciones web y para comparar nombres: sin acentos, en minúsculas y con guiones
/// («Ciudad de México» → <c>ciudad-de-mexico</c>). Dos escrituras de un mismo nombre dan el mismo identificador.
/// </summary>
public static class Slug
{
    public const int MaxLength = 100;

    /// <summary>Identificador del texto, o cadena vacía si no tiene ninguna letra ni número.</summary>
    public static string From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSeparator = false;

        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue; // El acento suelto que dejó la descomposición.
            }

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                Append(builder, character, ref pendingSeparator);
            }
            else if (character is >= 'A' and <= 'Z')
            {
                Append(builder, char.ToLowerInvariant(character), ref pendingSeparator);
            }
            else
            {
                pendingSeparator = builder.Length > 0;
            }
        }

        return builder.Length > MaxLength ? builder.ToString(0, MaxLength).TrimEnd('-') : builder.ToString();
    }

    private static void Append(StringBuilder builder, char character, ref bool pendingSeparator)
    {
        if (pendingSeparator)
        {
            builder.Append('-');
            pendingSeparator = false;
        }

        builder.Append(character);
    }
}
