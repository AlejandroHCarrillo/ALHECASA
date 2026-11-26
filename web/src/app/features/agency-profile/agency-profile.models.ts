import { environment } from '../../../environments/environment';

/**
 * La información de una inmobiliaria. La misma forma sirve para el perfil que edita su administrador y para lo que ve el público
 * (`GET /api/public/agencies/{id}`): los textos vacíos llegan como `null`.
 */
export interface AgencyProfile {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  description: string | null;
  website: string | null;
  facebookUrl: string | null;
  instagramUrl: string | null;
  /** Horario de atención en texto libre. */
  openingHours: string | null;
  street: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  hasLogo: boolean;
}

/** Lo que el administrador puede cambiar. El logotipo se sube aparte. */
export type UpdateAgencyProfileRequest = Omit<AgencyProfile, 'id' | 'hasLogo'>;

/** Mismos límites que la API (`AgencyProfileLimits`). */
export const AGENCY_PROFILE_LIMITS = {
  name: 200,
  email: 256,
  phone: 30,
  description: 1000,
  url: 200,
  openingHours: 200,
  street: 200,
  city: 100,
  state: 100,
  postalCode: 10,
} as const;

/** Formatos y tamaño aceptados para el logotipo (igual que las fotos de las propiedades). */
export const LOGO_LIMITS = {
  maxBytes: 5 * 1024 * 1024,
  acceptedTypes: ['image/jpeg', 'image/png', 'image/webp'],
} as const;

/**
 * Dirección del logotipo (la API lo entrega sin sesión). El archivo puede cambiar en la misma dirección, así que
 * `version` (p. ej. la hora de la última edición) obliga al navegador a pedirlo de nuevo.
 */
export function agencyLogoUrl(agencyId: string, version?: number): string {
  const url = `${environment.apiUrl}/public/agencies/${agencyId}/logo`;
  return version === undefined ? url : `${url}?v=${version}`;
}

/** Una sola línea con la dirección, sin los datos que faltan: «Av. Reforma 100, Ciudad de México, CDMX, 06600». */
export function formatAddress(agency: Pick<AgencyProfile, 'street' | 'city' | 'state' | 'postalCode'>): string {
  return [agency.street, agency.city, agency.state, agency.postalCode].filter((part) => !!part).join(', ');
}