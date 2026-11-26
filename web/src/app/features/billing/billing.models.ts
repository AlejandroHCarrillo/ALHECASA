import { CurrencyCode } from '../properties/property.models';
import { BillingPeriod, PlanKind } from '../plans/plan.models';

/** Publicaciones que permite la cuenta y cuántas usa. */
export interface ListingsAllowance {
  /** Nulo si una inmobiliaria no tiene plan contratado (sin tope). */
  planName: string | null;
  /** Nulo = ilimitado. */
  limit: number | null;
  used: number;
  /** Cuándo vence el plan contratado. Nulo en el plan gratuito y sin plan. */
  endsAt: string | null;
  /** Si es el plan gratuito de los particulares. */
  isFree: boolean;
}

/** Paquete de destacados vigente. */
export interface FeaturedAllowance {
  planName: string;
  limit: number | null;
  used: number;
  endsAt: string;
}

export interface AccountBilling {
  agencyId: string;
  agencyName: string;
  isIndividual: boolean;
  listings: ListingsAllowance;
  /** Nulo si no tiene un paquete de destacados vigente. */
  featured: FeaturedAllowance | null;
}

export interface Subscription {
  id: string;
  agencyId: string;
  planId: string;
  planName: string;
  kind: PlanKind;
  limit: number | null;
  period: BillingPeriod;
  amountPaid: number;
  currency: CurrencyCode;
  paymentReference: string | null;
  startsAt: string;
  endsAt: string;
  cancelledAt: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface AssignPlanRequest {
  planId: string;
  amountPaid: number | null;
  paymentReference: string | null;
}

/** «2 de 3», «12 de ilimitadas». */
export function describeUsage(used: number, limit: number | null): string {
  return `${used} de ${limit === null ? 'ilimitado' : limit}`;
}

/** Porcentaje usado para una barra de progreso; 0 si el tope es ilimitado. */
export function usagePercent(used: number, limit: number | null): number {
  return limit === null || limit <= 0 ? 0 : Math.min(100, Math.round((used / limit) * 100));
}

/** Tarjeta con la que se paga. Viaja solo en la compra: el servidor no la guarda. */
export interface PaymentCard {
  /** Solo dígitos. */
  number: string;
  holderName: string;
  expiryMonth: number;
  /** Cuatro cifras. */
  expiryYear: number;
  cvv: string;
}

export interface CheckoutRequest {
  planId: string;
  card: PaymentCard;
}

/** Tarjetas de prueba de la pasarela simulada (la real no las tendrá). */
export const TEST_CARDS = [
  { number: '4242 4242 4242 4242', label: 'Pago aprobado' },
  { number: '4000 0000 0000 0002', label: 'Tarjeta rechazada' },
  { number: '4000 0000 0000 9995', label: 'Fondos insuficientes' },
] as const;

/** Solo los dígitos de lo capturado («4242 4242-4242 4242» → «4242424242424242»). */
export function cardDigits(value: string): string {
  return value.replace(/\D/g, '');
}

/** Agrupa de cuatro en cuatro para mostrarlo mientras se escribe (máximo 19 dígitos). */
export function formatCardNumber(value: string): string {
  return (cardDigits(value).slice(0, 19).match(/.{1,4}/g) ?? []).join(' ');
}

/** 12 a 19 dígitos con dígito de control correcto (algoritmo de Luhn). */
export function isValidCardNumber(value: string): boolean {
  const digits = cardDigits(value);
  if (digits.length < 12 || digits.length > 19) {
    return false;
  }

  let sum = 0;
  let double = false;
  for (let i = digits.length - 1; i >= 0; i--) {
    let d = Number(digits[i]);
    if (double) {
      d *= 2;
      if (d > 9) {
        d -= 9;
      }
    }
    sum += d;
    double = !double;
  }

  return sum % 10 === 0;
}

/** «1228» o «12/28» → «12/28» mientras se escribe. */
export function formatExpiry(value: string): string {
  const digits = cardDigits(value).slice(0, 4);
  return digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
}

/** «MM/AA» → mes y año de cuatro cifras, o `null` si no es una fecha válida. */
export function parseExpiry(value: string): { month: number; year: number } | null {
  const match = /^(\d{2})\/(\d{2})$/.exec(value.trim());
  if (!match) {
    return null;
  }

  const month = Number(match[1]);
  return month >= 1 && month <= 12 ? { month, year: 2000 + Number(match[2]) } : null;
}

/** Una tarjeta vence al terminar el mes indicado. */
export function isExpired(month: number, year: number, now: Date = new Date()): boolean {
  return year < now.getFullYear() || (year === now.getFullYear() && month < now.getMonth() + 1);
}
