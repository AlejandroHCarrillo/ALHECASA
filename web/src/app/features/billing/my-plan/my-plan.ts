import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { ToastService } from '../../../shared/services/toast.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { BILLING_PERIOD_SPANS, BILLING_PERIOD_UNITS, PLAN_KIND_LABELS, Plan, PlanKind, describeLimit } from '../../plans/plan.models';
import { AccountBilling } from '../billing.models';
import { BillingService } from '../billing.service';
import { CheckoutModal } from '../checkout-modal/checkout-modal';
import { PlanSummary } from '../plan-summary/plan-summary';

/** El administrador de la inmobiliaria ve su plan y compra otro. El pago es simulado hasta integrar la pasarela. */
@Component({
  selector: 'app-my-plan',
  imports: [DecimalPipe, ErrorAlert, CheckoutModal, PlanSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './my-plan.html',
})
export class MyPlan implements OnInit {
  private readonly billing = inject(BillingService);
  private readonly toast = inject(ToastService);

  protected readonly kinds = Object.values(PlanKind);
  protected readonly kindLabels = PLAN_KIND_LABELS;
  protected readonly periodUnits = BILLING_PERIOD_UNITS;
  protected readonly periodSpans = BILLING_PERIOD_SPANS;
  protected readonly describeLimit = describeLimit;

  protected readonly account = signal<AccountBilling | null>(null);
  protected readonly plans = signal<Plan[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly kind = signal<PlanKind>(PlanKind.Listings);
  protected readonly selected = signal<Plan | null>(null);

  protected readonly visible = computed(() => this.plans().filter((p) => p.kind === this.kind()));

  ngOnInit(): void {
    this.load();
  }

  protected setKind(kind: PlanKind): void {
    this.kind.set(kind);
  }

  /** «Renovar» si es el mismo plan que ya tiene vigente; «Comprar» en cualquier otro caso. */
  protected isCurrent(plan: Plan): boolean {
    const account = this.account();
    if (!account) {
      return false;
    }

    return plan.kind === PlanKind.Listings
      ? !account.listings.isFree && account.listings.planName === plan.name
      : account.featured?.planName === plan.name;
  }

  protected buy(plan: Plan): void {
    this.selected.set(plan);
  }

  protected closeCheckout(): void {
    this.selected.set(null);
  }

  protected onPurchased(message: string): void {
    this.selected.set(null);
    this.toast.success(message);
    this.load();
  }

  private load(): void {
    this.error.set(null);
    let pending = 2;
    const done = (): void => {
      if (--pending === 0) {
        this.loading.set(false);
      }
    };

    this.billing.getMine().subscribe({
      next: (account) => {
        this.account.set(account);
        done();
      },
      error: (err: unknown) => {
        this.error.set(resolveApiError(err));
        done();
      },
    });

    this.billing.getAvailablePlans().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        done();
      },
      error: (err: unknown) => {
        this.error.set(resolveApiError(err));
        done();
      },
    });
  }
}
