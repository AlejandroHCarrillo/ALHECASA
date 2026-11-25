export const PropertyType = {
  House: 'House',
  Apartment: 'Apartment',
  Land: 'Land',
  Commercial: 'Commercial',
  Office: 'Office',
} as const;
export type PropertyType = (typeof PropertyType)[keyof typeof PropertyType];

export const OperationType = {
  Sale: 'Sale',
  Rent: 'Rent',
} as const;
export type OperationType = (typeof OperationType)[keyof typeof OperationType];

export const PropertyStatus = {
  Draft: 'Draft',
  Published: 'Published',
  Paused: 'Paused',
  Closed: 'Closed',
} as const;
export type PropertyStatus = (typeof PropertyStatus)[keyof typeof PropertyStatus];

export const CurrencyCode = {
  MXN: 'MXN',
  USD: 'USD',
} as const;
export type CurrencyCode = (typeof CurrencyCode)[keyof typeof CurrencyCode];

/** Amenidades. Los nombres son los de la API (`Amenity` del backend); cada propiedad tiene cero o más. */
export const Amenity = {
  Pool: 'Pool',
  Gym: 'Gym',
  Security24h: 'Security24h',
  Garden: 'Garden',
  Terrace: 'Terrace',
  Elevator: 'Elevator',
  Storage: 'Storage',
  ServiceRoom: 'ServiceRoom',
  AirConditioning: 'AirConditioning',
  CommonAreas: 'CommonAreas',
  Fireplace: 'Fireplace',
  Playground: 'Playground',
} as const;
export type Amenity = (typeof Amenity)[keyof typeof Amenity];

export const AMENITY_LABELS: Record<Amenity, string> = {
  Pool: 'Alberca',
  Gym: 'Gimnasio',
  Security24h: 'Seguridad 24 h',
  Garden: 'Jardín',
  Terrace: 'Terraza',
  Elevator: 'Elevador',
  Storage: 'Bodega',
  ServiceRoom: 'Cuarto de servicio',
  AirConditioning: 'Aire acondicionado',
  CommonAreas: 'Áreas comunes',
  Fireplace: 'Chimenea',
  Playground: 'Área de juegos',
};

/** Orden de los resultados. Los nombres son los de la API (`PropertySort`). */
export const PropertySort = {
  Recent: 'Recent',
  PriceAsc: 'PriceAsc',
  PriceDesc: 'PriceDesc',
  AreaDesc: 'AreaDesc',
} as const;
export type PropertySort = (typeof PropertySort)[keyof typeof PropertySort];

export const SORT_LABELS: Record<PropertySort, string> = {
  Recent: 'Más recientes',
  PriceAsc: 'Precio: menor a mayor',
  PriceDesc: 'Precio: mayor a menor',
  AreaDesc: 'Mayor superficie',
};

export const PROPERTY_TYPE_LABELS: Record<PropertyType, string> = {
  House: 'Casa',
  Apartment: 'Departamento',
  Land: 'Terreno',
  Commercial: 'Local comercial',
  Office: 'Oficina',
};

export const OPERATION_LABELS: Record<OperationType, string> = {
  Sale: 'Venta',
  Rent: 'Renta',
};

export const PROPERTY_STATUS_LABELS: Record<PropertyStatus, string> = {
  Draft: 'Borrador',
  Published: 'Publicada',
  Paused: 'Pausada',
  Closed: 'Cerrada (vendida o rentada)',
};

export const PROPERTY_STATUS_BADGES: Record<PropertyStatus, string> = {
  Draft: 'badge-ghost',
  Published: 'badge-success',
  Paused: 'badge-warning',
  Closed: 'badge-neutral',
};

/** Límites alineados con las columnas de la base y con PropertyRequestValidator del backend. */
export const PROPERTY_LIMITS = {
  title: 150,
  description: 2000,
  street: 200,
  neighborhood: 100,
  city: 100,
  state: 100,
  postalCode: 10,
  maxPrice: 9_999_999_999_999.99,
  maxArea: 99_999_999.99,
  maxRooms: 50,
  maxParking: 50,
} as const;

export interface Property {
  id: string;
  agencyId: string;
  agencyName: string | null;
  title: string;
  description: string | null;
  type: PropertyType;
  operation: OperationType;
  status: PropertyStatus;
  price: number;
  currency: CurrencyCode;
  street: string | null;
  neighborhood: string | null;
  city: string;
  state: string;
  postalCode: string | null;
  bedrooms: number | null;
  bathrooms: number | null;
  constructionArea: number | null;
  landArea: number | null;
  /** Ubicación exacta en el mapa; solo la ve el personal. Van juntas: las dos con valor o las dos nulas. */
  latitude: number | null;
  longitude: number | null;
  /** Cajones de estacionamiento (0 si no tiene). Nulo si no se capturó. */
  parkingSpaces: number | null;
  petsAllowed: boolean;
  furnished: boolean;
  amenities: Amenity[];
  /** Identificadores de las imágenes en orden de galería; la primera es la portada. */
  imageIds: string[];
  createdAt: string;
  updatedAt: string | null;
}

/** Imágenes por propiedad. Alineado con Property.MaxImages y PropertyImageRules del backend. */
export const IMAGE_LIMITS = {
  min: 1,
  max: 10,
  maxBytes: 5 * 1024 * 1024,
  acceptedTypes: ['image/jpeg', 'image/png', 'image/webp'],
} as const;

/** Campos que se capturan al crear o editar. */
export interface UpdatePropertyRequest {
  title: string;
  description: string | null;
  type: PropertyType;
  operation: OperationType;
  status: PropertyStatus;
  price: number;
  currency: CurrencyCode;
  street: string | null;
  neighborhood: string | null;
  city: string;
  state: string;
  postalCode: string | null;
  bedrooms: number | null;
  bathrooms: number | null;
  constructionArea: number | null;
  landArea: number | null;
  latitude: number | null;
  longitude: number | null;
  parkingSpaces: number | null;
  petsAllowed: boolean;
  furnished: boolean;
  amenities: Amenity[];
}

export interface CreatePropertyRequest extends UpdatePropertyRequest {
  /** Solo la envía el administrador de plataforma; el personal de una inmobiliaria siempre crea en la suya. */
  agencyId: string | null;
}
