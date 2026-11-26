import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { resolveApiError } from '../../../shared/utils/api-error';
import { AccountBilling, describeUsage, usagePercent } from '../../billing/billing.models';
import { BillingService } from '../../billing/billing.service';
import { AccountPlansModal } from '../account-plans-modal/account-plans-modal';

/** Cuentas con su plan de publicaciones y su paquete de destacados (solo administrador de plataforma). */
@Component({
  selector: 'app-subscription-list',
  imports: [DatePipe, ErrorAlert, AccountPlansModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './subscription-list.html',
})
export class SubscriptionList implements OnInit {
  private readonly billing = inject(BillingService);

  protected readonly describeUsage = describeUsage;
  protected readonly usagePercent = usagePercent;

  protected readonly accounts = signal<AccountBilling[]>([]);
  protected readonly loading = signal(false);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly search = signal('');
  protected readonly selected = signal<AccountBilling | null>(null);

  protected readonly visible = computed(() => {
    const term = this.search().trim().toLowerCase();
    return term ? this.accounts().filter((a) => a.agencyName.toLowerCase().includes(term)) : this.accounts();
  });

  ngOnInit(): void {
    this.reload();
  }

  protected setSearch(value: string): void {
    this.search.set(value);
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);

    this.billing.getAccounts().subscribe({
      next: (accounts) => {
        this.accounts.set(accounts);
        this.loading.set(false);
        this.loaded.set(true);
      },
      error: (err: unknown) => {
        this.error.set(resolveApiError(err));
        this.loading.set(false);
        this.loaded.set(true);
      },
    });
  }

  protected open(account: AccountBilling): void {
    this.selected.set(account);
  }

  protected close(): void {
    this.selected.set(null);
  }
}
