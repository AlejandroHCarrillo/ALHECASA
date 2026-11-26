import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { USER_ROLE_LABELS, UserRole } from '../../core/models/user-role';
import { AccountBilling } from '../billing/billing.models';
import { BillingService } from '../billing/billing.service';
import { PlanSummary } from '../billing/plan-summary/plan-summary';
import { DashboardStats } from './dashboard-stats/dashboard-stats';
import { AgencyDashboard } from './dashboard.models';
import { DashboardService } from './dashboard.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink, PlanSummary, DashboardStats],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-6">
      <div class="card bg-base-100 shadow">
        <div class="card-body">
          <h1 class="card-title text-2xl">Bienvenido, {{ auth.user()?.fullName }}</h1>
          <p class="text-base-content/70">
            Plataforma de gestión de compra, venta y renta de bienes raíces.
          </p>
          @if (auth.user(); as user) {
            <dl class="mt-2 grid grid-cols-1 gap-2 sm:grid-cols-2">
              <div>
                <dt class="text-xs uppercase text-base-content/60">Correo</dt>
                <dd>{{ user.email }}</dd>
              </div>
              <div>
                <dt class="text-xs uppercase text-base-content/60">Rol</dt>
                <dd>{{ user.isIndividual ? 'Particular' : roleLabels[user.role] }}</dd>
              </div>
              @if (user.isIndividual) {
                <div>
                  <dt class="text-xs uppercase text-base-content/60">Cuenta</dt>
                  <dd>Cuenta gratuita de particular</dd>
                </div>
              } @else {
                <div>
                  <dt class="text-xs uppercase text-base-content/60">Inmobiliaria</dt>
                  <dd>{{ user.agencyName ?? 'Sin inmobiliaria' }}</dd>
                </div>
              }
            </dl>
          }
        </div>
      </div>

      @if (dashboard(); as summary) {
        <app-dashboard-stats [dashboard]="summary" />
      }

      @if (billing(); as account) {
        <section class="space-y-3" aria-labelledby="home-plan-title">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <h2 id="home-plan-title" class="text-lg font-bold">Mi plan</h2>
            @if (canBuyPlans) {
              <a routerLink="/mi-plan" class="btn btn-primary btn-sm" data-testid="buy-plan">Comprar o cambiar de plan</a>
            }
          </div>
          <app-plan-summary [account]="account" />
        </section>
      }
    </div>
  `,
})
export class Home implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly billingService = inject(BillingService);
  private readonly dashboardService = inject(DashboardService);
  protected readonly roleLabels = USER_ROLE_LABELS;

  /** Lo contratado por la cuenta. Solo lo carga el personal de una inmobiliaria; nulo si no aplica o falla. */
  protected readonly billing = signal<AccountBilling | null>(null);

  /** Propiedades y solicitudes de la cuenta. Nulo si no aplica o falla: el resto de la página no depende de esto. */
  protected readonly dashboard = signal<AgencyDashboard | null>(null);

  /** Solo el administrador de la inmobiliaria compra; el resto de su personal solo consulta. */
  protected get canBuyPlans(): boolean {
    return this.auth.hasRole(UserRole.AgencyAdmin);
  }

  ngOnInit(): void {
    if (!this.auth.hasRole(UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller)) {
      return;
    }

    this.dashboardService.get().subscribe({
      next: (summary) => this.dashboard.set(summary),
      error: () => this.dashboard.set(null),
    });

    this.billingService.getMine().subscribe({
      next: (account) => this.billing.set(account),
      error: () => this.billing.set(null),
    });
  }
}
