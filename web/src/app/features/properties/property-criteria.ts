import { HttpParams } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { Amenity, CurrencyCode, PropertySort } from './property.models';

/**
 * Filtros avanzados y orden. Los comparten la portada pública y la lista del personal, igual que en la API
 * (`IPropertyCriteria`): un número vacío (`null`) o `false` significa «sin filtro».
 */
export interface AdvancedCriteria {
  minPrice: number | null;
  maxPrice: number | null;
  /** Moneda de los límites de precio. No hay conversión: solo se comparan propiedades en esta moneda. */
  currency: CurrencyCode;
  minBedrooms: number | null;
  minBathrooms: number | null;
  minParking: number | null;
  minArea: number | null;
  maxArea: number | null;
  petsAllowed: boolean;
  furnished: boolean;
  amenities: Amenity[];
  sort: PropertySort;
}

export const NO_ADVANCED_CRITERIA: AdvancedCriteria = {
  minPrice: null,
  maxPrice: null,
  currency: CurrencyCode.MXN,
  minBedrooms: null,
  minBathrooms: null,
  minParking: null,
  minArea: null,
  maxArea: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  sort: PropertySort.Recent,
};

/** Formulario tipado de los filtros avanzados. Se anida en el formulario de cada pantalla. */
export type AdvancedCriteriaForm = FormGroup<{
  minPrice: FormControl<number | null>;
  maxPrice: FormControl<number | null>;
  currency: FormControl<CurrencyCode>;
  minBedrooms: FormControl<number | null>;
  minBathrooms: FormControl<number | null>;
  minParking: FormControl<number | null>;
  minArea: FormControl<number | null>;
  maxArea: FormControl<number | null>;
  petsAllowed: FormControl<boolean>;
  furnished: FormControl<boolean>;
  amenities: FormControl<Amenity[]>;
  sort: FormControl<PropertySort>;
}>;

export function createAdvancedCriteriaForm(): AdvancedCriteriaForm {
  const c = NO_ADVANCED_CRITERIA;
  return new FormGroup({
    minPrice: new FormControl<number | null>(c.minPrice),
    maxPrice: new FormControl<number | null>(c.maxPrice),
    currency: new FormControl<CurrencyCode>(c.currency, { nonNullable: true }),
    minBedrooms: new FormControl<number | null>(c.minBedrooms),
    minBathrooms: new FormControl<number | null>(c.minBathrooms),
    minParking: new FormControl<number | null>(c.minParking),
    minArea: new FormControl<number | null>(c.minArea),
    maxArea: new FormControl<number | null>(c.maxArea),
    petsAllowed: new FormControl<boolean>(c.petsAllowed, { nonNullable: true }),
    furnished: new FormControl<boolean>(c.furnished, { nonNullable: true }),
    amenities: new FormControl<Amenity[]>([...c.amenities], { nonNullable: true }),
    sort: new FormControl<PropertySort>(c.sort, { nonNullable: true }),
  });
}

/** Un campo numérico vacío o inválido llega como null o como texto: solo cuentan los números finitos no negativos. */
export function cleanNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }
  const n = Number(value);
  return Number.isFinite(n) && n >= 0 ? n : null;
}

/** Lo que se aplicó, ya limpio: números inválidos fuera y amenidades sin repetir. */
export function normalizeAdvanced(value: Partial<AdvancedCriteria>): AdvancedCriteria {
  const base = NO_ADVANCED_CRITERIA;
  return {
    minPrice: cleanNumber(value.minPrice),
    maxPrice: cleanNumber(value.maxPrice),
    currency: value.currency ?? base.currency,
    minBedrooms: cleanNumber(value.minBedrooms),
    minBathrooms: cleanNumber(value.minBathrooms),
    minParking: cleanNumber(value.minParking),
    minArea: cleanNumber(value.minArea),
    maxArea: cleanNumber(value.maxArea),
    petsAllowed: value.petsAllowed === true,
    furnished: value.furnished === true,
    amenities: [...new Set(value.amenities ?? [])],
    sort: value.sort ?? base.sort,
  };
}

/** Cuántos filtros avanzados hay activos (el orden no cuenta: no reduce los resultados). */
export function countAdvanced(value: AdvancedCriteria): number {
  return (
    (value.minPrice !== null || value.maxPrice !== null ? 1 : 0) +
    (value.minBedrooms !== null ? 1 : 0) +
    (value.minBathrooms !== null ? 1 : 0) +
    (value.minParking !== null ? 1 : 0) +
    (value.minArea !== null || value.maxArea !== null ? 1 : 0) +
    (value.petsAllowed ? 1 : 0) +
    (value.furnished ? 1 : 0) +
    value.amenities.length
  );
}

/** Agrega a los parámetros de la API los filtros avanzados que tengan valor (la API ignora los que no vienen). */
export function appendAdvancedParams(params: HttpParams, value: AdvancedCriteria): HttpParams {
  const numbers: [string, number | null][] = [
    ['minPrice', value.minPrice],
    ['maxPrice', value.maxPrice],
    ['minBedrooms', value.minBedrooms],
    ['minBathrooms', value.minBathrooms],
    ['minParking', value.minParking],
    ['minArea', value.minArea],
    ['maxArea', value.maxArea],
  ];
  for (const [name, n] of numbers) {
    if (n !== null) {
      params = params.set(name, n);
    }
  }
  // La API exige la moneda cuando hay un límite de precio, y no la necesita sin él.
  if (value.minPrice !== null || value.maxPrice !== null) {
    params = params.set('currency', value.currency);
  }
  if (value.petsAllowed) {
    params = params.set('petsAllowed', true);
  }
  if (value.furnished) {
    params = params.set('furnished', true);
  }
  for (const amenity of value.amenities) {
    params = params.append('amenities', amenity);
  }
  if (value.sort !== PropertySort.Recent) {
    params = params.set('sort', value.sort);
  }
  return params;
}

/** Nombres de los parámetros de la dirección (URL) de la portada: en español, cortos y legibles al compartir el enlace. */
export const URL_KEYS = {
  operation: 'operacion',
  type: 'tipo',
  location: 'ubicacion',
  favoritesOnly: 'favoritas',
  minPrice: 'precioMin',
  maxPrice: 'precioMax',
  currency: 'moneda',
  minBedrooms: 'recamaras',
  minBathrooms: 'banos',
  minParking: 'estacionamientos',
  minArea: 'm2Min',
  maxArea: 'm2Max',
  petsAllowed: 'mascotas',
  furnished: 'amueblada',
  amenities: 'amenidades',
  sort: 'orden',
  page: 'pagina',
} as const;

/** Estado de los filtros avanzados en parámetros de dirección: solo los que tienen valor, para que el enlace sea corto. */
export function advancedToUrl(value: AdvancedCriteria): Record<string, string> {
  const out: Record<string, string> = {};
  const numbers: [string, number | null][] = [
    [URL_KEYS.minPrice, value.minPrice],
    [URL_KEYS.maxPrice, value.maxPrice],
    [URL_KEYS.minBedrooms, value.minBedrooms],
    [URL_KEYS.minBathrooms, value.minBathrooms],
    [URL_KEYS.minParking, value.minParking],
    [URL_KEYS.minArea, value.minArea],
    [URL_KEYS.maxArea, value.maxArea],
  ];
  for (const [key, n] of numbers) {
    if (n !== null) {
      out[key] = String(n);
    }
  }
  if (value.minPrice !== null || value.maxPrice !== null) {
    out[URL_KEYS.currency] = value.currency;
  }
  if (value.petsAllowed) {
    out[URL_KEYS.petsAllowed] = '1';
  }
  if (value.furnished) {
    out[URL_KEYS.furnished] = '1';
  }
  if (value.amenities.length > 0) {
    out[URL_KEYS.amenities] = value.amenities.join(',');
  }
  if (value.sort !== PropertySort.Recent) {
    out[URL_KEYS.sort] = value.sort;
  }
  return out;
}

/** Lo contrario de `advancedToUrl`. Todo lo que no se reconozca se descarta: la dirección la puede escribir cualquiera. */
export function advancedFromUrl(get: (key: string) => string | null): AdvancedCriteria {
  const currency = Object.values(CurrencyCode).find((c) => c === get(URL_KEYS.currency));
  const sort = Object.values(PropertySort).find((s) => s === get(URL_KEYS.sort));
  const amenities = (get(URL_KEYS.amenities) ?? '')
    .split(',')
    .map((a) => Object.values(Amenity).find((known) => known === a))
    .filter((a): a is Amenity => a !== undefined);

  return normalizeAdvanced({
    minPrice: cleanNumber(get(URL_KEYS.minPrice)),
    maxPrice: cleanNumber(get(URL_KEYS.maxPrice)),
    currency,
    minBedrooms: cleanNumber(get(URL_KEYS.minBedrooms)),
    minBathrooms: cleanNumber(get(URL_KEYS.minBathrooms)),
    minParking: cleanNumber(get(URL_KEYS.minParking)),
    minArea: cleanNumber(get(URL_KEYS.minArea)),
    maxArea: cleanNumber(get(URL_KEYS.maxArea)),
    petsAllowed: get(URL_KEYS.petsAllowed) === '1',
    furnished: get(URL_KEYS.furnished) === '1',
    amenities,
    sort,
  });
}
