import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { ToastService } from '../../../shared/services/toast.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { AccountBilling, Subscription } from '../../billing/billing.models';
import { BillingService } from '../../billing/billing.service';
import {
  BILLING_PERIOD_LABELS,
  BILLING_PERIOD_SPANS,
  PLAN_KIND_LABELS,
  PLAN_LIMITS,
  Plan,
  PlanAudience,
  PlanKind,
  describeLimit,
} from '../../plans/plan.models';
import { PlansService } from '../../plans/plans.service';

/**
 * Planes de una cuenta: lo que tiene vigente, el historial y el alta de una contratación.
 * Todavía no hay cobro en línea: el administrador registra aquí el pago que recibió.
 */
@Component({
  selector: 'app-account-plans-modal',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe, Modal, ErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-plans-modal.html',
})
export class AccountPlansModal implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly billing = inject(BillingService);
  private readonly plansService = inject(PlansService);
  private readonly toast = inject(ToastService);

  readonly account = input.required<AccountBilling>();
  /** Se emite cada vez que cambia algo de la cuenta, para que la pantalla refresque su tablero. */
  readonly changed = output<void>();
  readonly closed = output<void>();

  protected readonly kindLabels = PLAN_KIND_LABELS;
  protected readonly periodLabels = BILLING_PERIOD_LABELS;
  protected readonly spans = BILLING_PERIOD_SPANS;
  protected readonly describeLimit = describeLimit;
  protected readonly maxPrice = PLAN_LIMITS.maxPrice;
  protected readonly kinds = Object.values(PlanKind);

  protected readonly plans = signal<Plan[]>([]);
  protected readonly history = signal<Subscription[]>([]);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly cancelling = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly cancelTarget = signal<Subscription | null>(null);

  protected readonly form = this.fb.group({
    planId: ['', [Validators.required]],
    amountPaid: [null as number | null, [Validators.min(0), Validators.max(PLAN_LIMITS.maxPrice)]],
    paymentReference: ['', [Validators.maxLength(200)]],
  });

  /** Planes que se pueden contratar: activos, de pago y para este tipo de cuenta. */
  protected readonly offerable = computed(() =>
    this.plans().filter(
      (p) =>
        p.isActive &&
        !p.isFree &&
        (p.audience === PlanAudience.Any ||
          p.audience === (this.account().isIndividual ? PlanAudience.Individual : PlanAudience.Company)),
    ),
  );

  protected readonly offerableByKind = computed(() =>
    this.kinds
      .map((kind) => ({ kind, plans: this.offerable().filter((p) => p.kind === kind) }))
      .filter((group) => group.plans.length > 0),
  );

  protected readonly active = computed(() => this.history().filter((s) => s.isActive));

  private readonly selectedPlanId = signal('');
  protected readonly selectedPlan = computed(() => this.plans().find((p) => p.id === this.selectedPlanId()) ?? null);

  /** La contratación vigente del mismo tipo que el plan elegido: la nueva la reemplaza. */
  protected readonly replaced = computed(() => {
    const plan = this.selectedPlan();
    return plan ? (this.active().find((s) => s.kind === plan.kind) ?? null) : null;
  });

  constructor() {
    this.form.controls.planId.valueChanges.subscribe((id) => {
      this.selectedPlanId.set(id);
      // El monto sugerido es el precio del plan; el administrador lo cambia si cobró otra cosa.
      const plan = this.plans().find((p) => p.id === id);
      this.form.controls.amountPaid.setValue(plan ? plan.price : null);
    });
  }

  ngOnInit(): void {
    forkJoin({
      plans: this.plansService.list(),
      history: this.billing.getSubscriptions(this.account().agencyId),
    }).subscribe({
      next: ({ plans, history }) => {
        this.plans.set(plans);
        this.history.set(history);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(resolveApiError(err));
        this.loading.set(false);
      },
    });
  }

  protected hasError(control: 'planId' | 'amountPaid' | 'paymentReference'): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  protected statusOf(subscription: Subscription): string {
    if (subscription.isActive) {
      return 'Vigente';
    }

    return subscription.cancelledAt ? 'Cancelada' : 'Vencida';
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.billing
      .assign(this.account().agencyId, {
        planId: value.planId,
        amountPaid: value.amountPaid === null || (value.amountPaid as unknown) === '' ? null : Number(value.amountPaid),
        paymentReference: value.paymentReference.trim() || null,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.toast.success('Plan registrado correctamente.');
          this.form.reset({ planId: '', amountPaid: null, paymentReference: '' });
          this.refreshHistory();
          this.changed.emit();
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(resolveApiError(err));
        },
      });
  }

  protected askCancel(subscription: Subscription): void {
    this.error.set(null);
    this.cancelTarget.set(subscription);
  }

  protected dismissCancel(): void {
    this.cancelTarget.set(null);
  }

  protected confirmCancel(): void {
    const target = this.cancelTarget();
    if (!target) {
      return;
    }

    this.cancelling.set(true);
    this.billing.cancel(this.account().agencyId, target.id).subscribe({
      next: () => {
        this.cancelling.set(false);
        this.cancelTarget.set(null);
        this.toast.success('Contratación cancelada.');
        this.refreshHistory();
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.cancelling.set(false);
        this.cancelTarget.set(null);
        this.error.set(resolveApiError(err));
      },
    });
  }

  private refreshHistory(): void {
    this.billing.getSubscriptions(this.account().agencyId).subscribe({
      next: (history) => this.history.set(history),
      error: (err: unknown) => this.error.set(resolveApiError(err)),
    });
  }
}
