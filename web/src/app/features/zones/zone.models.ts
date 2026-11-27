/** Un enlace a una zona con propiedades publicadas: una ciudad, o una colonia de una ciudad. */
export interface ZoneLink {
  name: string;
  cityName: string;
  citySlug: string;
  /** Nulo si es una ciudad. */
  neighborhoodSlug: string | null;
  saleCount: number;
  rentCount: number;
}

/** Una ciudad, o una colonia de ella, con cuántas propiedades hay en venta y en renta, y las colonias de la ciudad. */
export interface Zone {
  cityName: string;
  citySlug: string;
  stateName: string;
  neighborhoodName: string | null;
  neighborhoodSlug: string | null;
  saleCount: number;
  rentCount: number;
  /** Las colonias de la ciudad (de la que se consulta, aunque la zona sea una colonia), con más propiedades primero. */
  neighborhoods: ZoneLink[];
}

/** La operación tal como aparece en la dirección: `/venta/...` o `/renta/...`. */
export type ZoneOperation = 'venta' | 'renta';

export const ZONE_OPERATIONS: readonly ZoneOperation[] = ['venta', 'renta'];

/** Ruta de la página pública de una zona, como comandos del enrutador (`['/venta', 'guadalajara', 'providencia']`). */
export function zoneCommands(operation: ZoneOperation, citySlug: string, neighborhoodSlug?: string | null): string[] {
  return neighborhoodSlug ? [`/${operation}`, citySlug, neighborhoodSlug] : [`/${operation}`, citySlug];
}

/** Ruta de la página pública de una zona como texto (`/venta/guadalajara/providencia`), para la dirección canónica. */
export function zonePath(operation: ZoneOperation, citySlug: string, neighborhoodSlug?: string | null): string {
  return zoneCommands(operation, citySlug, neighborhoodSlug)
    .map((part, index) => (index === 0 ? part : `/${encodeURIComponent(part)}`))
    .join('');
}

/** A qué operación lleva un enlace de zona: la que más propiedades tiene (en empate, la venta). */
export function mainOperation(link: Pick<ZoneLink, 'saleCount' | 'rentCount'>): ZoneOperation {
  return link.rentCount > link.saleCount ? 'renta' : 'venta';
}

export function operationCount(zone: Pick<Zone, 'saleCount' | 'rentCount'>, operation: ZoneOperation): number {
  return operation === 'venta' ? zone.saleCount : zone.rentCount;
}
