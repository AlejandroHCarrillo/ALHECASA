import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MortgageDisclaimer } from '../mortgage-disclaimer/mortgage-disclaimer';
import {
  MORTGAGE_LIMITS,
  affordability,
  amortizationSchedule,
  groupByYear,
  isValidLoan,
  loanSummary,
  upfrontCosts,
} from '../../utils/mortgage';

/** Monedas que acepta la calculadora (las mismas de las publicaciones). Solo cambian el formato de los montos. */
export const MORTGAGE_CURRENCIES = ['MXN', 'USD'] as const;
export type MortgageCurrency = (typeof MORTGAGE_CURRENCIES)[number];

type Mode = 'payment' | 'afford';
type ScheduleView = 'year' | 'month';

/** Valores de ejemplo, editables: no son las tasas ni las condiciones vigentes de ninguna institución. */
export const MORTGAGE_DEFAULTS = {
  downPaymentPercent: 20,
  annualRate: 10,
  years: 20,
  openingFeePercent: 1,
  closingCostsPercent: 5,
  debtRatioPercent: 30,
} as const;

/** Campos sin los cuales no hay crédito que calcular (los gastos opcionales no bloquean el resultado). */
const CORE_FIELDS = ['price', 'downPaymentAmount', 'downPaymentPercent', 'annualRate', 'years'] as const;

const round2 = (value: number): number => Math.round(value * 100) / 100;

const finiteOrNull = (value: number | null): number | null => (value !== null && Number.isFinite(value) ? value : null);

/**
 * Calculadora hipotecaria: la mensualidad con sus totales y gastos iniciales, la tabla de amortización y, al revés, cuánto crédito
 * alcanza con un ingreso. Todo se calcula en el navegador (ver `shared/utils/mortgage.ts`); no se envía ni se guarda nada.
 * Con `price` y `currency` se abre ya con los datos de una publicación.
 */
@Component({
  selector: 'app-mortgage-calculator',
  imports: [ReactiveFormsModule, MortgageDisclaimer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mortgage-calculator.html',
})
export class MortgageCalculator {
  private readonly destroyRef = inject(DestroyRef);

  /** Precio con el que se abre la calculadora (el de una publicación). Sin él, el precio se escribe. */
  readonly price = input<number | null>(null);
  readonly currency = input<MortgageCurrency | null>(null);
  /** Versión para una columna angosta, como la del detalle de una publicación: sin selector de moneda. */
  readonly compact = input(false);

  protected readonly limits = MORTGAGE_LIMITS;
  protected readonly currencies = MORTGAGE_CURRENCIES;

  protected readonly form = new FormGroup({
    price: new FormControl<number | null>(null),
    downPaymentAmount: new FormControl<number | null>(null),
    downPaymentPercent: new FormControl<number | null>(MORTGAGE_DEFAULTS.downPaymentPercent),
    annualRate: new FormControl<number | null>(MORTGAGE_DEFAULTS.annualRate),
    years: new FormControl<number | null>(MORTGAGE_DEFAULTS.years),
    openingFeePercent: new FormControl<number | null>(MORTGAGE_DEFAULTS.openingFeePercent),
    appraisal: new FormControl<number | null>(null),
    closingCostsPercent: new FormControl<number | null>(MORTGAGE_DEFAULTS.closingCostsPercent),
    monthlyInsurance: new FormControl<number | null>(null),
    currency: new FormControl<MortgageCurrency>('MXN', { nonNullable: true }),
  });

  /** Datos del cálculo inverso. Usa la tasa, el plazo y el seguro del formulario principal. */
  protected readonly affordForm = new FormGroup({
    monthlyIncome: new FormControl<number | null>(null),
    debtRatioPercent: new FormControl<number | null>(MORTGAGE_DEFAULTS.debtRatioPercent),
    downPayment: new FormControl<number | null>(null),
  });

  private readonly values = signal(this.form.getRawValue());
  private readonly affordValues = signal(this.affordForm.getRawValue());

  protected readonly mode = signal<Mode>('payment');
  protected readonly showSchedule = signal(false);
  protected readonly scheduleView = signal<ScheduleView>('year');

  protected readonly currencyCode = computed(() => this.values().currency);
  private readonly formatter = computed(
    () => new Intl.NumberFormat('es-MX', { style: 'currency', currency: this.currencyCode(), maximumFractionDigits: 2 }),
  );

  private readonly priceValue = computed(() => finiteOrNull(this.values().price) ?? 0);
  private readonly downPaymentValue = computed(() => finiteOrNull(this.values().downPaymentAmount) ?? 0);
  private readonly insuranceValue = computed(() => Math.max(0, finiteOrNull(this.values().monthlyInsurance) ?? 0));

  /** Errores por campo, en español. Un campo vacío no es un error (el precio vacío solo deja sin resultado). */
  protected readonly errors = computed(() => {
    const v = this.values();
    const a = this.affordValues();
    const errors: Record<string, string> = {};
    const price = finiteOrNull(v.price);
    const out = (value: number | null) => finiteOrNull(value);

    if (price !== null && price <= 0) {
      errors['price'] = 'El precio debe ser mayor que cero.';
    }

    const down = out(v.downPaymentAmount);
    if (down !== null && down < 0) {
      errors['downPaymentAmount'] = 'El enganche no puede ser negativo.';
    } else if (down !== null && price !== null && price > 0 && down >= price) {
      errors['downPaymentAmount'] = 'El enganche debe ser menor que el precio.';
    }

    const percent = out(v.downPaymentPercent);
    if (percent !== null && (percent < 0 || percent >= 100)) {
      errors['downPaymentPercent'] = 'Debe estar entre 0 y 99.99 %.';
    }

    const rate = out(v.annualRate);
    if (rate === null) {
      errors['annualRate'] = 'Escribe la tasa anual.';
    } else if (rate < 0 || rate > MORTGAGE_LIMITS.maxAnnualRate) {
      errors['annualRate'] = `La tasa debe estar entre 0 y ${MORTGAGE_LIMITS.maxAnnualRate} %.`;
    }

    const years = out(v.years);
    if (years === null) {
      errors['years'] = 'Escribe el plazo en años.';
    } else if (years < MORTGAGE_LIMITS.minYears || years > MORTGAGE_LIMITS.maxYears) {
      errors['years'] = `El plazo debe estar entre ${MORTGAGE_LIMITS.minYears} y ${MORTGAGE_LIMITS.maxYears} años.`;
    }

    const opening = out(v.openingFeePercent);
    if (opening !== null && (opening < 0 || opening > 100)) {
      errors['openingFeePercent'] = 'Debe estar entre 0 y 100 %.';
    }
    const closing = out(v.closingCostsPercent);
    if (closing !== null && (closing < 0 || closing > 100)) {
      errors['closingCostsPercent'] = 'Debe estar entre 0 y 100 %.';
    }
    const appraisal = out(v.appraisal);
    if (appraisal !== null && appraisal < 0) {
      errors['appraisal'] = 'El avalúo no puede ser negativo.';
    }
    const insurance = out(v.monthlyInsurance);
    if (insurance !== null && insurance < 0) {
      errors['monthlyInsurance'] = 'El seguro no puede ser negativo.';
    }

    const income = out(a.monthlyIncome);
    if (income !== null && income <= 0) {
      errors['monthlyIncome'] = 'El ingreso debe ser mayor que cero.';
    }
    const ratio = out(a.debtRatioPercent);
    if (ratio === null) {
      errors['debtRatioPercent'] = 'Escribe qué parte de tu ingreso destinarías.';
    } else if (ratio <= 0 || ratio > 100) {
      errors['debtRatioPercent'] = 'Debe ser mayor que 0 y hasta 100 %.';
    }
    const affordDown = out(a.downPayment);
    if (affordDown !== null && affordDown < 0) {
      errors['affordDownPayment'] = 'El enganche no puede ser negativo.';
    }

    return errors;
  });

  private readonly terms = computed(() => ({
    principal: this.priceValue() - this.downPaymentValue(),
    annualRate: finiteOrNull(this.values().annualRate) ?? Number.NaN,
    years: finiteOrNull(this.values().years) ?? Number.NaN,
  }));

  /** El crédito se puede calcular: hay precio, enganche menor al precio, tasa y plazo válidos, y ningún dato del crédito tiene error. */
  protected readonly canCalculate = computed(() => {
    const errors = this.errors();
    return isValidLoan(this.terms()) && !CORE_FIELDS.some((field) => errors[field]);
  });

  protected readonly summary = computed(() => (this.canCalculate() ? loanSummary(this.terms()) : null));
  /** Mensualidad con el seguro mensual, si se indicó. */
  protected readonly totalMonthly = computed(() => (this.summary()?.monthlyPayment ?? 0) + this.insuranceValue());

  protected readonly upfront = computed(() => {
    const v = this.values();
    return upfrontCosts({
      price: this.priceValue(),
      downPayment: this.downPaymentValue(),
      openingFeePercent: v.openingFeePercent ?? undefined,
      appraisal: v.appraisal ?? undefined,
      closingCostsPercent: v.closingCostsPercent ?? undefined,
    });
  });

  /** Capital contra intereses, como porcentaje de todo lo que se paga, para la barra. */
  protected readonly split = computed(() => {
    const summary = this.summary();
    if (!summary) {
      return { principal: 0, interest: 0 };
    }

    const principal = (summary.principal / summary.totalPaid) * 100;
    return { principal, interest: 100 - principal };
  });

  /** La tabla solo se calcula al abrirla: hasta 480 filas que casi nadie lee. */
  protected readonly schedule = computed(() => (this.showSchedule() && this.canCalculate() ? amortizationSchedule(this.terms()) : []));
  protected readonly yearly = computed(() => groupByYear(this.schedule()));

  protected readonly affordability = computed(() => {
    const v = this.values();
    const a = this.affordValues();
    return affordability({
      monthlyIncome: finiteOrNull(a.monthlyIncome) ?? 0,
      debtRatioPercent: finiteOrNull(a.debtRatioPercent) ?? 0,
      annualRate: finiteOrNull(v.annualRate) ?? Number.NaN,
      years: finiteOrNull(v.years) ?? Number.NaN,
      downPayment: finiteOrNull(a.downPayment) ?? 0,
      monthlyInsurance: this.insuranceValue(),
    });
  });

  /** Si hay un precio, si el crédito calculado alcanzaría para esa propiedad. */
  protected readonly fits = computed(() => {
    const result = this.affordability();
    const price = this.priceValue();
    return result && price > 0 ? result.maxPrice >= price : null;
  });

  constructor() {
    const { price, downPaymentAmount, downPaymentPercent } = this.form.controls;

    // Enganche en monto y en porcentaje, siempre sincronizados. Cambiar el precio conserva el porcentaje.
    downPaymentAmount.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((amount) => {
      const base = finiteOrNull(price.value);
      const value = finiteOrNull(amount);
      if (base !== null && base > 0 && value !== null) {
        downPaymentPercent.setValue(round2((value / base) * 100), { emitEvent: false });
      }
    });
    downPaymentPercent.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.amountFromPercent());
    price.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.amountFromPercent());

    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.values.set(this.form.getRawValue()));
    this.affordForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.affordValues.set(this.affordForm.getRawValue()));

    // Los datos de una publicación precargan el formulario, y lo vuelven a precargar si cambia de publicación.
    effect(() => {
      const initialPrice = this.price();
      const initialCurrency = this.currency();
      untracked(() => {
        if (initialCurrency) {
          this.form.controls.currency.setValue(initialCurrency);
        }
        if (initialPrice !== null && Number.isFinite(initialPrice) && initialPrice > 0) {
          this.form.controls.price.setValue(initialPrice);
        }
      });
    });
  }

  protected setMode(mode: Mode): void {
    this.mode.set(mode);
  }

  protected toggleSchedule(): void {
    this.showSchedule.update((open) => !open);
  }

  protected setScheduleView(view: ScheduleView): void {
    this.scheduleView.set(view);
  }

  protected money(value: number): string {
    return this.formatter().format(value);
  }

  protected percent(value: number): string {
    return `${value.toFixed(1)} %`;
  }

  private amountFromPercent(): void {
    const base = finiteOrNull(this.form.controls.price.value);
    const percent = finiteOrNull(this.form.controls.downPaymentPercent.value);
    if (base !== null && base > 0 && percent !== null) {
      this.form.controls.downPaymentAmount.setValue(round2((base * percent) / 100), { emitEvent: false });
    }
  }
}
