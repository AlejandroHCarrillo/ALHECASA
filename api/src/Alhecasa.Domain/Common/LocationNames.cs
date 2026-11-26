using System.Globalization;
using System.Text;

namespace Alhecasa.Domain.Common;

/// <summary>Limpieza de los nombres de estados, ciudades y colonias que escribe una persona.</summary>
public static class LocationNames
{
    private static readonly HashSet<string> Connectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "del", "la", "las", "el", "los", "y", "e", "en", "a"
    };

    /// <summary>
    /// Quita espacios de más y, si el texto viene todo en minúsculas o todo en mayúsculas (más de cuatro letras: «CDMX» y
    /// «NL» se respetan), lo pasa a «Tipo Título» («polanco» → «Polanco», «COL. DEL VALLE» → «Col. del Valle»).
    /// Un texto con mayúsculas y minúsculas mezcladas se deja como se escribió.
    /// </summary>
    public static string Tidy(string value)
    {
        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join(' ', words);

        var letters = text.Count(char.IsLetter);
        var hasLower = text.Any(char.IsLower);
        var hasUpper = text.Any(char.IsUpper);
        var uniform = !(hasLower && hasUpper);
        if (!uniform || (hasUpper && letters <= 4))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < words.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            var word = words[index].ToLower(CultureInfo.InvariantCulture);
            if (index > 0 && Connectors.Contains(word))
            {
                builder.Append(word);
            }
            else
            {
                builder.Append(char.ToUpper(word[0], CultureInfo.InvariantCulture)).Append(word, 1, word.Length - 1);
            }
        }

        return builder.ToString();
    }
}
