import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { resolveApiError } from '../../../shared/utils/api-error';
import { CurrencyCode } from '../../properties/property.models';
import {
  BILLING_PERIOD_LABELS,
  BillingPeriod,
  PLAN_AUDIENCE_LABELS,
  PLAN_KIND_LABELS,
  PLAN_LIMITS,
  Plan,
  PlanAudience,
  PlanKind,
  SUGGESTED_LIMITS,
  SavePlanRequest,
} from '../plan.models';
import { PlansService } from '../plans.service';

/** Un campo vacío no es cero: sin precio capturado todavía no se sabe si el plan es gratuito. */
function isZero(value: number | string | null): boolean {
  return value !== null && value !== '' && Number(value) === 0;
}

/**
 * Modal compartido para crear (plan = null) y editar un plan.
 * Un precio de 0 es el plan gratuito de los particulares: solo de publicaciones, para particulares y sin vencimiento.
 */
@Component({
  selector: 'app-plan-form-modal',
  imports: [ReactiveFormsModule, Modal, ErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './plan-form-modal.html',
})
export class PlanFormModal implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly plansService = inject(PlansService);
  private readonly destroyRef = inject(DestroyRef);

  readonly plan = input<Plan | null>(null);
  readonly saved = output<string>();
  readonly cancelled = output<void>();

  protected readonly kinds = Object.values(PlanKind);
  protected readonly audiences = Object.values(PlanAudience);
  protected readonly periods = Object.values(BillingPeriod);
  protected readonly currencies = Object.values(CurrencyCode);
  protected readonly kindLabels = PLAN_KIND_LABELS;
  protected readonly audienceLabels = PLAN_AUDIENCE_LABELS;
  protected readonly periodLabels = BILLING_PERIOD_LABELS;
  protected readonly limits = PLAN_LIMITS;

  protected readonly isEdit = computed(() => this.plan() !== null);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  // Reflejo de los controles que cambian lo que se muestra.
  protected readonly isFree = signal(false);
  protected readonly unlimited = signal(false);
  protected readonly selectedKind = signal<PlanKind>(PlanKind.Listings);
  protected readonly suggestedLimits = computed(() => SUGGESTED_LIMITS[this.selectedKind()]);

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(PLAN_LIMITS.nameMaxLength)]],
    description: ['', [Validators.maxLength(PLAN_LIMITS.descriptionMaxLength)]],
    kind: [PlanKind.Listings as PlanKind, [Validators.required]],
    audience: [PlanAudience.Company as PlanAudience, [Validators.required]],
    unlimited: [false],
    limit: [100 as number | null, [Validators.required, Validators.min(1), Validators.max(PLAN_LIMITS.maxLimit)]],
    period: [BillingPeriod.Month as BillingPeriod, [Validators.required]],
    price: [null as number | null, [Validators.required, Validators.min(0), Validators.max(PLAN_LIMITS.maxPrice)]],
    currency: [CurrencyCode.MXN as CurrencyCode, [Validators.required]],
    isActive: [true],
  });

  constructor() {
    this.form.controls.price.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.applyFreeRules());
    this.form.controls.kind.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((kind) => this.selectedKind.set(kind));
    this.form.controls.unlimited.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((value) => this.applyUnlimited(value));
  }

  ngOnInit(): void {
    const plan = this.plan();

    if (plan) {
      this.form.patchValue({
        name: plan.name,
        description: plan.description ?? '',
        kind: plan.kind,
        audience: plan.audience,
        unlimited: plan.limit === null,
        limit: plan.limit,
        period: plan.period ?? BillingPeriod.Month,
        price: plan.price,
        currency: plan.currency,
        isActive: plan.isActive,
      });
    }

    this.selectedKind.set(this.form.controls.kind.value);
    this.applyUnlimited(this.form.controls.unlimited.value);
    this.applyFreeRules();
  }

  protected hasError(control: 'name' | 'description' | 'limit' | 'price'): boolean {
    const c = this.form.controls[control];
    return c.touched && c.invalid;
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const free = isZero(value.price);
    const request: SavePlanRequest = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      // Un plan gratuito es siempre de publicaciones, para particulares y sin vencimiento (lo exige el backend).
      kind: free ? PlanKind.Listings : value.kind,
      audience: free ? PlanAudience.Individual : value.audience,
      limit: value.unlimited ? null : Number(value.limit),
      period: free ? null : value.period,
      price: Number(value.price),
      currency: value.currency,
      isActive: free ? true : value.isActive,
    };

    const plan = this.plan();
    const request$: Observable<Plan> = plan ? this.plansService.update(plan.id, request) : this.plansService.create(request);

    this.submitting.set(true);
    this.error.set(null);

    request$.subscribe({
      next: () => {
        this.submitting.set(false);
        this.saved.emit(plan ? 'Plan actualizado correctamente.' : 'Plan creado correctamente.');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(
          resolveApiError(err, { conflict: 'Ya existe un plan con ese nombre, o ya hay un plan gratuito (edítalo en lugar de crear otro).' }),
        );
      },
    });
  }

  /** «Ilimitado» apaga el campo del tope: no se valida ni se envía. */
  private applyUnlimited(unlimited: boolean): void {
    this.unlimited.set(unlimited);
    const limit = this.form.controls.limit;
    if (unlimited) {
      limit.disable({ emitEvent: false });
    } else {
      limit.enable({ emitEvent: false });
    }
  }

  /**
   * Precio 0 = plan gratuito: se fijan el tipo y el público, y no hay periodo. Un plan gratuito que ya existe
   * no puede volverse de pago ni desactivarse (el backend lo rechaza).
   */
  private applyFreeRules(): void {
    const free = isZero(this.form.controls.price.value);
    this.isFree.set(free);

    const { kind, audience, period, isActive, price } = this.form.controls;
    if (free) {
      kind.setValue(PlanKind.Listings, { emitEvent: false });
      audience.setValue(PlanAudience.Individual, { emitEvent: false });
      this.selectedKind.set(PlanKind.Listings);
      kind.disable({ emitEvent: false });
      audience.disable({ emitEvent: false });
      period.disable({ emitEvent: false });
      isActive.setValue(true, { emitEvent: false });
      isActive.disable({ emitEvent: false });
    } else {
      kind.enable({ emitEvent: false });
      audience.enable({ emitEvent: false });
      period.enable({ emitEvent: false });
      isActive.enable({ emitEvent: false });
    }

    if (this.plan()?.isFree) {
      price.disable({ emitEvent: false });
    }
  }
}
