/**
 * Aritmética de un crédito hipotecario de pagos fijos (sistema francés). Son funciones puras y sin Angular: las usa la calculadora
 * y se prueban solas. Todo es una estimación informativa: no incluye el CAT ni las condiciones reales de ninguna institución.
 *
 * Las tasas se reciben en porcentaje anual (10 = 10 %) y los plazos en años. Los resultados conservan toda la precisión: redondear
 * a centavos es cosa de quien los muestra.
 */

/** Límites aceptados. Fuera de ellos las funciones no calculan (ver `isValidLoan`): así nunca se devuelve `NaN` ni `Infinity`. */
export const MORTGAGE_LIMITS = {
  minYears: 1,
  maxYears: 40,
  maxAnnualRate: 100,
} as const;

export interface LoanTerms {
  /** Monto del crédito (precio menos enganche). */
  principal: number;
  /** Tasa de interés anual, en porcentaje. */
  annualRate: number;
  years: number;
}

export interface ScheduleRow {
  /** Número de pago, desde 1. */
  month: number;
  payment: number;
  /** Parte del pago que reduce la deuda. */
  principal: number;
  interest: number;
  /** Deuda pendiente después de este pago. */
  balance: number;
}

export interface YearRow {
  year: number;
  payment: number;
  principal: number;
  interest: number;
  balance: number;
}

export interface LoanSummary {
  principal: number;
  /** Cuota mensual de capital e intereses (sin seguro). */
  monthlyPayment: number;
  totalPaid: number;
  totalInterest: number;
  /** Cuánto se paga de intereses por cada peso de crédito. */
  interestRatio: number;
}

/** Cada concepto es opcional: lo que no se indique no se cobra. */
export interface CostInputs {
  price: number;
  downPayment: number;
  /** Comisión por apertura, en porcentaje del crédito. */
  openingFeePercent?: number;
  /** Avalúo, monto fijo. */
  appraisal?: number;
  /** Escrituración y gastos notariales, en porcentaje del precio. */
  closingCostsPercent?: number;
}

export interface UpfrontCosts {
  downPayment: number;
  openingFee: number;
  appraisal: number;
  closingCosts: number;
  /** Lo que hay que tener listo antes de la primera mensualidad. */
  total: number;
}

export interface AffordabilityInputs {
  monthlyIncome: number;
  /** Qué parte del ingreso se puede destinar al crédito, en porcentaje (por ejemplo 30). */
  debtRatioPercent: number;
  annualRate: number;
  years: number;
  /** Enganche con el que se cuenta. */
  downPayment?: number;
  /** Seguro mensual fijo: se descuenta de la cuota que se puede pagar. */
  monthlyInsurance?: number;
}

export interface Affordability {
  /** Cuota máxima de capital e intereses (ya sin el seguro). */
  maxMonthlyPayment: number;
  maxLoan: number;
  /** Crédito máximo más el enganche con el que se cuenta. */
  maxPrice: number;
}

const MONTHS_PER_YEAR = 12;

const isFinitePositive = (value: number): boolean => Number.isFinite(value) && value > 0;

/** Un crédito es calculable si el monto es positivo, la tasa está entre 0 y 100 y el plazo entre 1 y 40 años. */
export function isValidLoan({ principal, annualRate, years }: LoanTerms): boolean {
  return (
    isFinitePositive(principal) &&
    Number.isFinite(annualRate) &&
    annualRate >= 0 &&
    annualRate <= MORTGAGE_LIMITS.maxAnnualRate &&
    Number.isFinite(years) &&
    years >= MORTGAGE_LIMITS.minYears &&
    years <= MORTGAGE_LIMITS.maxYears
  );
}

const paymentsCount = (years: number): number => Math.round(years * MONTHS_PER_YEAR);

const monthlyRate = (annualRate: number): number => annualRate / 100 / MONTHS_PER_YEAR;

/** Factor `r / (1 - (1+r)^-n)`: cuánto se paga al mes por cada peso prestado. Con tasa 0 la fórmula divide entre cero, y es simplemente `1/n`. */
function paymentFactor(annualRate: number, years: number): number {
  const n = paymentsCount(years);
  const r = monthlyRate(annualRate);
  return r === 0 ? 1 / n : r / (1 - Math.pow(1 + r, -n));
}

/** Cuota mensual fija de capital e intereses. `NaN` si el crédito no es calculable (ver `isValidLoan`). */
export function monthlyPayment(terms: LoanTerms): number {
  return isValidLoan(terms) ? terms.principal * paymentFactor(terms.annualRate, terms.years) : Number.NaN;
}

/**
 * Tabla de amortización mes a mes. El último pago absorbe el redondeo para que la deuda termine exactamente en cero.
 * Vacía si el crédito no es calculable.
 */
export function amortizationSchedule(terms: LoanTerms): ScheduleRow[] {
  if (!isValidLoan(terms)) {
    return [];
  }

  const n = paymentsCount(terms.years);
  const r = monthlyRate(terms.annualRate);
  const payment = monthlyPayment(terms);
  const rows: ScheduleRow[] = [];
  let balance = terms.principal;

  for (let month = 1; month <= n; month++) {
    const interest = balance * r;
    const isLast = month === n;
    const principal = isLast ? balance : payment - interest;
    balance = isLast ? 0 : balance - principal;
    rows.push({ month, payment: principal + interest, principal, interest, balance });
  }

  return rows;
}

/** La tabla resumida por año de crédito (el año 1 son los pagos 1 a 12). */
export function groupByYear(rows: readonly ScheduleRow[]): YearRow[] {
  const years: YearRow[] = [];
  for (const row of rows) {
    const year = Math.ceil(row.month / MONTHS_PER_YEAR);
    let current = years[year - 1];
    if (!current) {
      current = { year, payment: 0, principal: 0, interest: 0, balance: row.balance };
      years.push(current);
    }

    current.payment += row.payment;
    current.principal += row.principal;
    current.interest += row.interest;
    current.balance = row.balance;
  }

  return years;
}

/** Totales del crédito. `null` si no es calculable. */
export function loanSummary(terms: LoanTerms): LoanSummary | null {
  if (!isValidLoan(terms)) {
    return null;
  }

  const payment = monthlyPayment(terms);
  const totalPaid = payment * paymentsCount(terms.years);
  const totalInterest = totalPaid - terms.principal;
  return { principal: terms.principal, monthlyPayment: payment, totalPaid, totalInterest, interestRatio: totalInterest / terms.principal };
}

/** Lo que se paga al principio: enganche más los gastos opcionales. El crédito es el precio menos el enganche. */
export function upfrontCosts(inputs: CostInputs): UpfrontCosts {
  const nonNegative = (value: number | undefined): number => (value !== undefined && Number.isFinite(value) && value > 0 ? value : 0);
  const downPayment = nonNegative(inputs.downPayment);
  const loan = Math.max(0, nonNegative(inputs.price) - downPayment);
  const openingFee = (loan * nonNegative(inputs.openingFeePercent)) / 100;
  const appraisal = nonNegative(inputs.appraisal);
  const closingCosts = (nonNegative(inputs.price) * nonNegative(inputs.closingCostsPercent)) / 100;

  return { downPayment, openingFee, appraisal, closingCosts, total: downPayment + openingFee + appraisal + closingCosts };
}

/**
 * Cálculo inverso: cuánto crédito alcanza con un ingreso. La cuota máxima es el ingreso por el porcentaje de endeudamiento (menos el
 * seguro), y el crédito es el valor presente de esa cuota. `null` si los datos no permiten calcularlo.
 */
export function affordability(inputs: AffordabilityInputs): Affordability | null {
  const { monthlyIncome, debtRatioPercent, annualRate, years } = inputs;
  const monthlyInsurance = Math.max(0, inputs.monthlyInsurance ?? 0);
  const downPayment = Math.max(0, inputs.downPayment ?? 0);

  if (
    !isFinitePositive(monthlyIncome) ||
    !Number.isFinite(debtRatioPercent) ||
    debtRatioPercent <= 0 ||
    debtRatioPercent > 100 ||
    !isValidLoan({ principal: 1, annualRate, years }) ||
    !Number.isFinite(monthlyInsurance) ||
    !Number.isFinite(downPayment)
  ) {
    return null;
  }

  const maxMonthlyPayment = Math.max(0, (monthlyIncome * debtRatioPercent) / 100 - monthlyInsurance);
  const maxLoan = maxMonthlyPayment / paymentFactor(annualRate, years);
  return { maxMonthlyPayment, maxLoan, maxPrice: maxLoan + downPayment };
}
