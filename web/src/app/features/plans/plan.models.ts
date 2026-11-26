import { CurrencyCode } from '../properties/property.models';

/** Qué otorga un plan: un tope de publicaciones, o un paquete de anuncios destacados. */
export const PlanKind = {
  Listings: 'Listings',
  Featured: 'Featured',
} as const;
export type PlanKind = (typeof PlanKind)[keyof typeof PlanKind];

export const PLAN_KIND_LABELS: Record<PlanKind, string> = {
  Listings: 'Publicaciones',
  Featured: 'Destacados',
};

export const BillingPeriod = {
  Day: 'Day',
  Week: 'Week',
  Month: 'Month',
  Year: 'Year',
} as const;
export type BillingPeriod = (typeof BillingPeriod)[keyof typeof BillingPeriod];

/** Una contratación dura exactamente un periodo. */
export const BILLING_PERIOD_LABELS: Record<BillingPeriod, string> = {
  Day: 'Día',
  Week: 'Semana',
  Month: 'Mes',
  Year: 'Año',
};

/** «por día», «por semana»… para leerlo junto al precio. */
export const BILLING_PERIOD_UNITS: Record<BillingPeriod, string> = {
  Day: 'por día',
  Week: 'por semana',
  Month: 'por mes',
  Year: 'por año',
};

/** «un día», «una semana»… para decir cuánto dura una contratación. */
export const BILLING_PERIOD_SPANS: Record<BillingPeriod, string> = {
  Day: 'un día',
  Week: 'una semana',
  Month: 'un mes',
  Year: 'un año',
};

export const PlanAudience = {
  Individual: 'Individual',
  Company: 'Company',
  Any: 'Any',
} as const;
export type PlanAudience = (typeof PlanAudience)[keyof typeof PlanAudience];

export const PLAN_AUDIENCE_LABELS: Record<PlanAudience, string> = {
  Individual: 'Particulares',
  Company: 'Inmobiliarias',
  Any: 'Cualquier cuenta',
};

/** Topes sugeridos al capturar un plan (se puede escribir otro valor). */
export const SUGGESTED_LIMITS: Record<PlanKind, readonly number[]> = {
  Listings: [3, 100, 200, 300, 500],
  Featured: [1, 3, 5, 10, 50, 100, 200, 300, 500, 1000],
};

/** Debe coincidir con `Plan.MaxLimit` y `Plan.MaxPrice` del backend. */
export const PLAN_LIMITS = {
  maxLimit: 100_000,
  maxPrice: 9_999_999_999.99,
  nameMaxLength: 100,
  descriptionMaxLength: 500,
} as const;

export interface Plan {
  id: string;
  name: string;
  description: string | null;
  kind: PlanKind;
  audience: PlanAudience;
  /** Publicaciones o destacados a la vez. `null` = ilimitado. */
  limit: number | null;
  /** `null` solo en el plan gratuito, que no vence. */
  period: BillingPeriod | null;
  price: number;
  currency: CurrencyCode;
  isActive: boolean;
  isFree: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface SavePlanRequest {
  name: string;
  description: string | null;
  kind: PlanKind;
  audience: PlanAudience;
  limit: number | null;
  period: BillingPeriod | null;
  price: number;
  currency: CurrencyCode;
  isActive: boolean;
}

/** «Ilimitadas» / «Hasta 100 publicaciones» / «3 destacados a la vez». */
export function describeLimit(kind: PlanKind, limit: number | null): string {
  if (kind === PlanKind.Listings) {
    return limit === null ? 'Publicaciones ilimitadas' : `${limit} ${limit === 1 ? 'publicación' : 'publicaciones'}`;
  }

  return limit === null ? 'Destacados ilimitados' : `${limit} ${limit === 1 ? 'destacado' : 'destacados'} a la vez`;
}
