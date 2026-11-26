using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Infrastructure.Persistence.Seeding;

/// <summary>
/// Inventario de ejemplo para la inmobiliaria demo: variado en tipo, operación, estado, moneda y ubicación.
/// Las coordenadas (los dos últimos valores) son aproximadas, de la colonia o la zona, no de una dirección real.
/// </summary>
internal static class SampleProperties
{
    public static IReadOnlyList<PropertyData> All { get; } =
    [
        new("Casa moderna con alberca en Lomas de Chapultepec",
            "Casa de tres niveles con acabados de lujo, alberca climatizada, jardín y cuarto de servicio.",
            PropertyType.House, OperationType.Sale, PropertyStatus.Published, 18_500_000m, CurrencyCode.MXN,
            "Paseo de las Palmas 450", "Lomas de Chapultepec", "Ciudad de México", "CDMX", "11000",
            4, 5, 520m, 600m, 19.4228m, -99.2146m,
            ParkingSpaces: 4, PetsAllowed: true, Amenities: Amenity.Pool | Amenity.Garden | Amenity.ServiceRoom | Amenity.Security24h),

        new("Departamento con vista al parque en Polanco",
            "Departamento luminoso en piso 8, balcón con vista al parque, dos cajones de estacionamiento y bodega.",
            PropertyType.Apartment, OperationType.Sale, PropertyStatus.Published, 9_200_000m, CurrencyCode.MXN,
            "Av. Horacio 1020", "Polanco", "Ciudad de México", "CDMX", "11560",
            2, 2, 140m, null, 19.4333m, -99.1962m,
            ParkingSpaces: 2, Amenities: Amenity.Elevator | Amenity.Storage | Amenity.Gym | Amenity.Security24h),

        new("Loft amueblado en la Roma Norte",
            "Loft de un ambiente con cocina equipada, cerca de cafés y transporte. Ideal para ejecutivos.",
            PropertyType.Apartment, OperationType.Rent, PropertyStatus.Published, 22_000m, CurrencyCode.MXN,
            "Calle Colima 215", "Roma Norte", "Ciudad de México", "CDMX", "06700",
            1, 1, 65m, null, 19.4194m, -99.1627m,
            ParkingSpaces: 0, PetsAllowed: true, Furnished: true, Amenities: Amenity.Elevator | Amenity.AirConditioning),

        new("Casa en condominio con seguridad en Zapopan",
            "Casa en privada con vigilancia 24 horas, áreas verdes, ludoteca y gimnasio.",
            PropertyType.House, OperationType.Sale, PropertyStatus.Published, 5_800_000m, CurrencyCode.MXN,
            "Camino a Santa Lucía 120", "Valle Real", "Zapopan", "Jalisco", "45019",
            3, 3, 230m, 160m, 20.7021m, -103.4290m,
            ParkingSpaces: 2, PetsAllowed: true, Amenities: Amenity.Security24h | Amenity.CommonAreas | Amenity.Playground | Amenity.Gym | Amenity.Garden),

        new("Terreno plano con uso de suelo habitacional en Querétaro",
            "Terreno de 1,200 m² con todos los servicios, en esquina y a cinco minutos de la autopista.",
            PropertyType.Land, OperationType.Sale, PropertyStatus.Published, 3_600_000m, CurrencyCode.MXN,
            "Circuito Juriquilla 88", "Juriquilla", "Querétaro", "Querétaro", "76230",
            null, null, null, 1_200m, 20.7000m, -100.4450m,
            ParkingSpaces: null),

        new("Local comercial en avenida principal de Monterrey",
            "Local a pie de calle con 8 metros de frente, bodega y baño. Alto flujo peatonal.",
            PropertyType.Commercial, OperationType.Rent, PropertyStatus.Published, 48_000m, CurrencyCode.MXN,
            "Av. Constitución 1500", "Centro", "Monterrey", "Nuevo León", "64000",
            null, 1, 110m, null, 25.6714m, -100.3097m,
            ParkingSpaces: 2, Amenities: Amenity.AirConditioning),

        new("Oficina corporativa en torre con amenidades",
            "Piso completo de 320 m² con vista panorámica, sala de juntas, recepción y seis cajones de estacionamiento.",
            PropertyType.Office, OperationType.Rent, PropertyStatus.Paused, 4_800m, CurrencyCode.USD,
            "Av. Santa Fe 495", "Santa Fe", "Ciudad de México", "CDMX", "05348",
            null, 3, 320m, null, 19.3598m, -99.2606m,
            ParkingSpaces: 6, Amenities: Amenity.Elevator | Amenity.AirConditioning | Amenity.Security24h),

        new("Penthouse frente al mar en Playa del Carmen",
            "Penthouse con terraza privada, jacuzzi, acceso a playa y amenidades de resort.",
            PropertyType.Apartment, OperationType.Sale, PropertyStatus.Published, 650_000m, CurrencyCode.USD,
            "Quinta Avenida 300", "Zona Hotelera", "Playa del Carmen", "Quintana Roo", "77710",
            3, 3, 210m, null, 20.6296m, -87.0739m,
            ParkingSpaces: 2, Furnished: true, Amenities: Amenity.Pool | Amenity.Gym | Amenity.Terrace | Amenity.Security24h | Amenity.Elevator | Amenity.CommonAreas),

        new("Casa de campo en Valle de Bravo (en preparación)",
            "Casa con chimenea, terreno arbolado y vista al lago. Pendiente de captura de fotografías.",
            PropertyType.House, OperationType.Sale, PropertyStatus.Draft, 7_900_000m, CurrencyCode.MXN,
            null, "Avandaro", "Valle de Bravo", "Estado de México", "51200",
            5, 4, 380m, 2_500m, 19.1873m, -100.1280m,
            ParkingSpaces: 3, PetsAllowed: true, Amenities: Amenity.Fireplace | Amenity.Garden | Amenity.Terrace),

        new("Departamento de dos recámaras rentado en Guadalajara",
            "Departamento cerca de la Minerva con cocina integrada y estacionamiento. Ya fue rentado.",
            PropertyType.Apartment, OperationType.Rent, PropertyStatus.Closed, 15_500m, CurrencyCode.MXN,
            "Av. Vallarta 2500", "Arcos Vallarta", "Guadalajara", "Jalisco", "44130",
            2, 1, 85m, null, 20.6745m, -103.3925m,
            ParkingSpaces: 1),
    ];
}
