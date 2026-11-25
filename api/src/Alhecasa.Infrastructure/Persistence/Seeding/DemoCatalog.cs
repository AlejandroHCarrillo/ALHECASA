using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Infrastructure.Persistence.Seeding;

/// <summary>Una inmobiliaria de demostración con su personal y sus anuncios. Las fotos están en <c>SeedData/catalog-images/NN</c>.</summary>
internal sealed record DemoAgencyData(
    string Name,
    string Email,
    string Phone,
    IReadOnlyList<DemoUserData> Users,
    IReadOnlyList<PropertyData> Properties,
    /// <summary>Número de la primera carpeta de fotos de esta inmobiliaria (los anuncios usan carpetas consecutivas).</summary>
    int FirstImageFolder);

internal sealed record DemoUserData(string FirstName, string LastName, string Email, UserRole Role);

/// <summary>
/// Catálogo de demostración adicional (dos inmobiliarias, su personal y 10 anuncios publicados con fotos).
/// Todo es ficticio: nombres, correos <c>.local</c>, direcciones y teléfonos. Las coordenadas son aproximadas, de la zona.
/// Se siembra solo si se configura <c>Seed:DemoCatalog:Password</c> (user-secrets o variable de entorno).
/// </summary>
internal static class DemoCatalog
{
    public static IReadOnlyList<DemoAgencyData> Agencies { get; } =
    [
        new("Goldita Bienes Raíces", "contacto@goldita.local", "5555550201",
        [
            new("Gloria", "Dorado", "admin@goldita.local", UserRole.AgencyAdmin),
            new("Sergio", "Ramírez", "supervisor@goldita.local", UserRole.Supervisor),
            new("Valeria", "Montes", "vendedor@goldita.local", UserRole.Seller),
        ],
        [
            new("Casa colonial restaurada en el centro de Mérida",
                "Casa del siglo XIX completamente restaurada, con techos altos, pisos de pasta originales, patio central con plantas tropicales y alberca pequeña.",
                PropertyType.House, OperationType.Sale, PropertyStatus.Published, 6_400_000m, CurrencyCode.MXN,
                "Calle 60 480", "Centro", "Mérida", "Yucatán", "97000",
                4, 3, 380m, 450m, 20.9674m, -89.5926m,
            ParkingSpaces: 2, PetsAllowed: true, Amenities: Amenity.Pool | Amenity.Garden | Amenity.Terrace),

            new("Departamento con terraza en la Condesa",
                "Departamento de dos recámaras con terraza amplia, pisos de madera y cocina integral, en edificio art déco con vigilancia. Se admiten mascotas.",
                PropertyType.Apartment, OperationType.Rent, PropertyStatus.Published, 28_500m, CurrencyCode.MXN,
                "Av. Tamaulipas 120", "Condesa", "Ciudad de México", "CDMX", "06140",
                2, 2, 95m, null, 19.4115m, -99.1730m,
            ParkingSpaces: 1, PetsAllowed: true, Amenities: Amenity.Terrace | Amenity.Elevator | Amenity.Security24h),

            new("Casa con jardín y vista al volcán en San Andrés Cholula",
                "Casa de dos niveles en fraccionamiento cerrado, con jardín, cocina abierta, cuarto de servicio y vista al Popocatépetl.",
                PropertyType.House, OperationType.Sale, PropertyStatus.Published, 3_950_000m, CurrencyCode.MXN,
                "Camino Real a Cholula 77", "Lomas de Angelópolis", "San Andrés Cholula", "Puebla", "72810",
                3, 2, 210m, 300m, 19.0531m, -98.3028m,
            ParkingSpaces: 2, PetsAllowed: true, Amenities: Amenity.Garden | Amenity.ServiceRoom | Amenity.Security24h | Amenity.CommonAreas),

            new("Local comercial en plaza de Puebla",
                "Local a pie de calle con fachada de cristal, bodega, baño y cajones de estacionamiento al frente. Plaza con alto flujo de visitantes.",
                PropertyType.Commercial, OperationType.Rent, PropertyStatus.Published, 35_000m, CurrencyCode.MXN,
                "Blvd. Atlixcáyotl 2301", "Reserva Territorial Atlixcáyotl", "Puebla", "Puebla", "72190",
                null, 2, 85m, null, 19.0032m, -98.2330m,
            ParkingSpaces: 4, Amenities: Amenity.AirConditioning),

            new("Terreno cerca de la playa en la costa de Yucatán",
                "Terreno plano de 1,500 m² a unos metros de la playa, con acceso por camino de terracería y servicio de luz cercano. Ideal para casa de descanso.",
                PropertyType.Land, OperationType.Sale, PropertyStatus.Published, 180_000m, CurrencyCode.USD,
                null, "Chicxulub Puerto", "Progreso", "Yucatán", "97330",
                null, null, null, 1_500m, 21.3010m, -89.5980m,
            ParkingSpaces: null),
        ],
        FirstImageFolder: 1),

        new("Osito Real State", "contacto@osito.local", "5555550202",
        [
            new("Oscar", "Peña", "admin@osito.local", UserRole.AgencyAdmin),
            new("Silvia", "Cortés", "supervisor@osito.local", UserRole.Supervisor),
            new("Víctor", "Lozano", "vendedor@osito.local", UserRole.Seller),
        ],
        [
            new("Casa familiar contemporánea en Providencia",
                "Casa de cantera y madera con sala-comedor abierto al jardín, chimenea, cuarto de servicio y cochera para tres autos.",
                PropertyType.House, OperationType.Sale, PropertyStatus.Published, 7_200_000m, CurrencyCode.MXN,
                "Av. Providencia 2850", "Providencia", "Guadalajara", "Jalisco", "44630",
                4, 4, 340m, 280m, 20.6958m, -103.3893m,
            ParkingSpaces: 3, PetsAllowed: true, Amenities: Amenity.Fireplace | Amenity.Garden | Amenity.ServiceRoom | Amenity.Terrace),

            new("Departamento con vista al mar en la Zona Hotelera de Cancún",
                "Departamento amueblado en piso alto, con balcón al mar Caribe, cocina equipada y acceso a alberca y playa del condominio.",
                PropertyType.Apartment, OperationType.Rent, PropertyStatus.Published, 2_200m, CurrencyCode.USD,
                "Blvd. Kukulcán km 9", "Zona Hotelera", "Cancún", "Quintana Roo", "77500",
                2, 2, 130m, null, 21.1390m, -86.7480m,
            ParkingSpaces: 1, Furnished: true, Amenities: Amenity.Pool | Amenity.Gym | Amenity.Terrace | Amenity.AirConditioning | Amenity.Elevator | Amenity.Security24h),

            new("Oficina corporativa en San Pedro Garza García",
                "Oficina de 150 m² en planta abierta con sala de juntas de cristal, vista a la montaña, recepción compartida y cuatro cajones de estacionamiento.",
                PropertyType.Office, OperationType.Rent, PropertyStatus.Published, 38_000m, CurrencyCode.MXN,
                "Av. Lázaro Cárdenas 2400", "Valle Oriente", "San Pedro Garza García", "Nuevo León", "66269",
                null, 2, 150m, null, 25.6516m, -100.3560m,
            ParkingSpaces: 4, Amenities: Amenity.Elevator | Amenity.AirConditioning | Amenity.Security24h | Amenity.CommonAreas),

            new("Casa en privada con seguridad en El Refugio, Querétaro",
                "Casa nueva de dos plantas en privada con caseta de vigilancia y áreas verdes. Recámara principal con vestidor y patio trasero.",
                PropertyType.House, OperationType.Sale, PropertyStatus.Published, 4_650_000m, CurrencyCode.MXN,
                "Paseo del Refugio 55", "El Refugio", "Querétaro", "Querétaro", "76146",
                3, 3, 190m, 150m, 20.6370m, -100.3750m,
            ParkingSpaces: 2, PetsAllowed: true, Amenities: Amenity.Security24h | Amenity.Garden | Amenity.CommonAreas | Amenity.Playground),

            new("Terreno campestre con viñedo en Valle de Guadalupe",
                "Terreno de media hectárea con viñedo joven, encinos y acceso por camino de terracería. Pozo y energía eléctrica a pie de predio.",
                PropertyType.Land, OperationType.Sale, PropertyStatus.Published, 320_000m, CurrencyCode.USD,
                null, "Valle de Guadalupe", "Ensenada", "Baja California", "22750",
                null, null, null, 5_000m, 32.0850m, -116.6190m,
            ParkingSpaces: null),
        ],
        FirstImageFolder: 6),
    ];
}