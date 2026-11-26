import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { resolveApiError } from '../../../shared/utils/api-error';
import { BILLING_PERIOD_SPANS, BILLING_PERIOD_UNITS, PLAN_KIND_LABELS, Plan, PlanKind, describeLimit } from '../../plans/plan.models';
import {
  AccountBilling,
  TEST_CARDS,
  cardDigits,
  formatCardNumber,
  formatExpiry,
  isExpired,
  isValidCardNumber,
  parseExpiry,
} from '../billing.models';
import { BillingService } from '../billing.service';

/** Un campo con solo espacios cuenta como vacío. */
const notBlankValidator = (control: AbstractControl<string>): ValidationErrors | null => (control.value.trim() ? null : { required: true });

const cardNumberValidator = (control: AbstractControl<string>): ValidationErrors | null =>
  control.value && !isValidCardNumber(control.value) ? { cardNumber: true } : null;

/** Vence al terminar el mes indicado; «MM/AA» con un mes real. */
const expiryValidator = (control: AbstractControl<string>): ValidationErrors | null => {
  if (!control.value) {
    return null;
  }

  const expiry = parseExpiry(control.value);
  if (!expiry) {
    return { expiryFormat: true };
  }

  return isExpired(expiry.month, expiry.year) ? { expired: true } : null;
};

/**
 * Pago de un plan con tarjeta. La pasarela es una simulación: no se cobra nada, y los números de prueba se muestran
 * en pantalla. Cuando exista la pasarela real, el formulario de tarjeta lo reemplaza el componente de su proveedor.
 */
@Component({
  selector: 'app-checkout-modal',
  imports: [ReactiveFormsModule, DecimalPipe, Modal, ErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './checkout-modal.html',
})
export class CheckoutModal {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly billing = inject(BillingService);

  readonly plan = input.required<Plan>();
  readonly account = input.required<AccountBilling>();
  readonly purchased = output<string>();
  readonly cancelled = output<void>();

  protected readonly kindLabels = PLAN_KIND_LABELS;
  protected readonly periodUnits = BILLING_PERIOD_UNITS;
  protected readonly periodSpans = BILLING_PERIOD_SPANS;
  protected readonly describeLimit = describeLimit;
  protected readonly testCards = TEST_CARDS;

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.group({
    number: ['', [Validators.required, cardNumberValidator]],
    holderName: ['', [notBlankValidator, Validators.maxLength(100)]],
    expiry: ['', [Validators.required, expiryValidator]],
    cvv: ['', [Validators.required, Validators.pattern(/^\d{3,4}$/)]],
  });

  /** Lo que ya tiene contratado del mismo tipo, que esta compra reemplaza. */
  protected readonly replaces = computed(() => {
    const account = this.account();
    return this.plan().kind === PlanKind.Listings
      ? account.listings.isFree
        ? null
        : account.listings.planName
      : (account.featured?.planName ?? null);
  });

  /** Comprar un paquete de destacados quita el destacado de los anuncios actuales. */
  protected readonly clearsFeatured = computed(() => this.plan().kind === PlanKind.Featured && (this.account().featured?.used ?? 0) > 0);

  protected hasError(control: 'number' | 'holderName' | 'expiry' | 'cvv'): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  /** Da formato mientras se escribe, sin mover el cursor de lugar de forma molesta. */
  protected formatNumber(): void {
    const control = this.form.controls.number;
    control.setValue(formatCardNumber(control.value), { emitEvent: false });
  }

  protected formatExpiry(): void {
    const control = this.form.controls.expiry;
    control.setValue(formatExpiry(control.value), { emitEvent: false });
  }

  protected useTestCard(number: string): void {
    this.form.patchValue({
      number,
      holderName: this.form.controls.holderName.value || 'TITULAR DE PRUEBA',
      expiry: this.form.controls.expiry.value || '12/99',
      cvv: this.form.controls.cvv.value || '123',
    });
    this.form.markAllAsTouched();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const expiry = parseExpiry(value.expiry)!;

    this.submitting.set(true);
    this.error.set(null);

    this.billing
      .checkout({
        planId: this.plan().id,
        card: {
          number: cardDigits(value.number),
          holderName: value.holderName.trim(),
          expiryMonth: expiry.month,
          expiryYear: expiry.year,
          cvv: value.cvv,
        },
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.purchased.emit(`Pago aprobado. Ya tienes el plan «${this.plan().name}».`);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(resolveApiError(err));
        },
      });
  }
}
