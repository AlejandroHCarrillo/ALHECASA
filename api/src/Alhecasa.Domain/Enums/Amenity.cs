namespace Alhecasa.Domain.Enums;

/// <summary>
/// Amenidades de una propiedad. Es una máscara de bits: una propiedad guarda varias en un solo entero, y buscar
/// «las que tengan todas estas» es una sola comparación en la base. Cada valor es una potencia de dos y no se renumera
/// nunca (lo guardado en la base depende de ello); las nuevas se agregan al final con el siguiente bit.
/// </summary>
[Flags]
public enum Amenity
{
    None = 0,
    Pool = 1,
    Gym = 2,
    Security24h = 4,
    Garden = 8,
    Terrace = 16,
    Elevator = 32,
    Storage = 64,
    ServiceRoom = 128,
    AirConditioning = 256,
    CommonAreas = 512,
    Fireplace = 1024,
    Playground = 2048
}

/// <summary>Conversión entre la máscara que se guarda y la lista que usa la API.</summary>
public static class AmenityFlags
{
    /// <summary>Todos los valores individuales (sin <see cref="Amenity.None"/>), en orden.</summary>
    public static IReadOnlyList<Amenity> All { get; } =
        Enum.GetValues<Amenity>().Where(a => a != Amenity.None).ToArray();

    /// <summary>Un valor individual definido: potencia de dos y existente. No acepta combinaciones ni números sueltos.</summary>
    public static bool IsSingle(Amenity amenity) => All.Contains(amenity);

    public static Amenity Combine(IEnumerable<Amenity>? amenities) =>
        amenities is null ? Amenity.None : amenities.Aggregate(Amenity.None, (mask, a) => mask | a);

    public static IReadOnlyList<Amenity> ToList(Amenity mask) => All.Where(a => mask.HasFlag(a)).ToList();
}
