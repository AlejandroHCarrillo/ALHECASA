/** Rangos válidos en grados; alineados con Property.SetCoordinates y PropertyRequestValidator del backend. */
export const COORDINATE_LIMITS = { maxLatitude: 90, maxLongitude: 180 } as const;

export interface Coordinates {
  latitude: number;
  longitude: number;
}

/**
 * Lee unas coordenadas escritas como «19.4326, -99.1332» (así las copia Google Maps; también se acepta separarlas con espacio o `;`).
 * Devuelve `null` si el texto está vacío (la propiedad no tiene ubicación) y `'invalid'` si no son dos números dentro de rango.
 */
export function parseCoordinates(text: string): Coordinates | null | 'invalid' {
  const trimmed = text.trim();
  if (trimmed === '') {
    return null;
  }

  const match = /^(-?\d+(?:\.\d+)?)\s*[,;\s]\s*(-?\d+(?:\.\d+)?)$/.exec(trimmed);
  if (!match) {
    return 'invalid';
  }

  const latitude = Number(match[1]);
  const longitude = Number(match[2]);
  if (Math.abs(latitude) > COORDINATE_LIMITS.maxLatitude || Math.abs(longitude) > COORDINATE_LIMITS.maxLongitude) {
    return 'invalid';
  }

  return { latitude, longitude };
}

/** Texto editable de unas coordenadas (cadena vacía si no hay ubicación). */
export function formatCoordinates(latitude: number | null, longitude: number | null): string {
  return latitude === null || longitude === null ? '' : `${latitude}, ${longitude}`;
}