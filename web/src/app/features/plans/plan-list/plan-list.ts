import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { ToastService } from '../../../shared/services/toast.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { PlanFormModal } from '../plan-form-modal/plan-form-modal';
import {
  BILLING_PERIOD_LABELS,
  BILLING_PERIOD_UNITS,
  PLAN_AUDIENCE_LABELS,
  PLAN_KIND_LABELS,
  Plan,
  PlanKind,
  describeLimit,
} from '../plan.models';
import { PlansService } from '../plans.service';

type KindFilter = PlanKind | '';

/** Catálogo de planes de pago (solo administrador de plataforma). */
@Component({
  selector: 'app-plan-list',
  imports: [DecimalPipe, ErrorAlert, Modal, PlanFormModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './plan-list.html',
})
export class PlanList implements OnInit {
  private readonly plansService = inject(PlansService);
  private readonly toast = inject(ToastService);

  protected readonly kinds = Object.values(PlanKind);
  protected readonly kindLabels = PLAN_KIND_LABELS;
  protected readonly audienceLabels = PLAN_AUDIENCE_LABELS;
  protected readonly periodLabels = BILLING_PERIOD_LABELS;
  protected readonly periodUnits = BILLING_PERIOD_UNITS;
  protected readonly describeLimit = describeLimit;

  protected readonly plans = signal<Plan[]>([]);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly kindFilter = signal<KindFilter>('');

  protected readonly visible = computed(() => {
    const kind = this.kindFilter();
    return kind ? this.plans().filter((p) => p.kind === kind) : this.plans();
  });

  // Estado de los modales
  protected readonly showForm = signal(false);
  protected readonly formPlan = signal<Plan | null>(null);
  protected readonly deleteTarget = signal<Plan | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  protected setKindFilter(value: string): void {
    this.kindFilter.set(value as KindFilter);
  }

  protected reload(): void {
    this.loading.set(true);
    this.listError.set(null);

    this.plansService.list().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.loading.set(false);
        this.loaded.set(true);
      },
      error: (err: unknown) => {
        this.listError.set(resolveApiError(err));
        this.loading.set(false);
        this.loaded.set(true);
      },
    });
  }

  // --- Alta / edición ---
  protected openForm(plan: Plan | null): void {
    this.formPlan.set(plan);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
    this.formPlan.set(null);
  }

  protected onSaved(message: string): void {
    this.closeForm();
    this.toast.success(message);
    this.reload();
  }

  // --- Eliminación (lógica) ---
  protected askDelete(plan: Plan): void {
    this.deleteError.set(null);
    this.deleteTarget.set(plan);
  }

  protected cancelDelete(): void {
    this.deleteTarget.set(null);
    this.deleteError.set(null);
  }

  protected confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    this.plansService.delete(target.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.deleteTarget.set(null);
        this.toast.success('Plan eliminado correctamente.');
        this.reload();
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.deleteError.set(resolveApiError(err));
      },
    });
  }
}
