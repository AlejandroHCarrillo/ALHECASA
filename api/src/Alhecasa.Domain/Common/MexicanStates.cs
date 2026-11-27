namespace Alhecasa.Domain.Common;

/// <summary>
/// Los 32 estados de México con sus abreviaturas y nombres alternos comunes. Sirve para unir escrituras como «CDMX»,
/// «DF» y «Ciudad de México» en un solo estado. Un estado que no está en la lista (por ejemplo, de otro país) se acepta tal cual.
/// </summary>
public static class MexicanStates
{
    private static readonly (string Name, string[] Aliases)[] All =
    [
        ("Aguascalientes", ["AGS"]),
        ("Baja California", ["BC", "B C"]),
        ("Baja California Sur", ["BCS"]),
        ("Campeche", ["CAMP"]),
        ("Chiapas", ["CHIS"]),
        ("Chihuahua", ["CHIH"]),
        ("Ciudad de México", ["CDMX", "DF", "D F", "Distrito Federal", "Mexico City", "CD MX", "CD de Mexico"]),
        ("Coahuila", ["COAH", "Coahuila de Zaragoza"]),
        ("Colima", ["COL"]),
        ("Durango", ["DGO"]),
        ("Estado de México", ["EDOMEX", "Edo Mex", "Edo de Mexico", "Edo de Mex", "Mexico", "MEX", "State of Mexico"]),
        ("Guanajuato", ["GTO"]),
        ("Guerrero", ["GRO"]),
        ("Hidalgo", ["HGO"]),
        ("Jalisco", ["JAL"]),
        ("Michoacán", ["MICH", "Michoacan de Ocampo"]),
        ("Morelos", ["MOR"]),
        ("Nayarit", ["NAY"]),
        ("Nuevo León", ["NL", "N L"]),
        ("Oaxaca", ["OAX"]),
        ("Puebla", ["PUE"]),
        ("Querétaro", ["QRO", "Queretaro de Arteaga"]),
        ("Quintana Roo", ["QROO", "Q Roo"]),
        ("San Luis Potosí", ["SLP", "S L P"]),
        ("Sinaloa", ["SIN"]),
        ("Sonora", ["SON"]),
        ("Tabasco", ["TAB"]),
        ("Tamaulipas", ["TAMPS", "TAM"]),
        ("Tlaxcala", ["TLAX"]),
        ("Veracruz", ["VER", "Veracruz de Ignacio de la Llave"]),
        ("Yucatán", ["YUC"]),
        ("Zacatecas", ["ZAC"])
    ];

    private static readonly Dictionary<string, string> BySlug = BuildIndex();

    /// <summary>Los nombres oficiales de los estados, en orden alfabético.</summary>
    public static IReadOnlyList<string> Names { get; } = All.Select(s => s.Name).OrderBy(Slug.From, StringComparer.Ordinal).ToList();

    /// <summary>El nombre oficial del estado que el texto nombra o abrevia, o null si no es uno de los 32.</summary>
    public static string? Canonical(string? text)
    {
        var slug = Slug.From(text);
        return slug.Length > 0 && BySlug.TryGetValue(slug, out var name) ? name : null;
    }

    private static Dictionary<string, string> BuildIndex()
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, aliases) in All)
        {
            index[Slug.From(name)] = name;
            foreach (var alias in aliases)
            {
                index.TryAdd(Slug.From(alias), name);
            }
        }

        return index;
    }
}
