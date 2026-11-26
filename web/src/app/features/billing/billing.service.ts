import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Plan } from '../plans/plan.models';
import { AccountBilling, AssignPlanRequest, CheckoutRequest, Subscription } from './billing.models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/billing`;

  /** El plan de la cuenta del usuario y lo que lleva usado (personal de una inmobiliaria o particular). */
  getMine(): Observable<AccountBilling> {
    return this.http.get<AccountBilling>(`${this.url}/me`);
  }

  /** Los planes que la cuenta puede comprar (administrador de la inmobiliaria). */
  getAvailablePlans(): Observable<Plan[]> {
    return this.http.get<Plan[]>(`${this.url}/plans`);
  }

  /** Compra un plan con tarjeta: se cobra el precio del plan (pasarela simulada por ahora) y queda contratado. */
  checkout(request: CheckoutRequest): Observable<Subscription> {
    return this.http.post<Subscription>(`${this.url}/checkout`, request);
  }

  /** Lo contratado y lo usado por todas las cuentas (administrador de plataforma). */
  getAccounts(): Observable<AccountBilling[]> {
    return this.http.get<AccountBilling[]>(`${this.url}/accounts`);
  }

  getSubscriptions(agencyId: string): Observable<Subscription[]> {
    return this.http.get<Subscription[]>(`${this.url}/accounts/${agencyId}/subscriptions`);
  }

  assign(agencyId: string, request: AssignPlanRequest): Observable<Subscription> {
    return this.http.post<Subscription>(`${this.url}/accounts/${agencyId}/subscriptions`, request);
  }

  cancel(agencyId: string, subscriptionId: string): Observable<Subscription> {
    return this.http.post<Subscription>(`${this.url}/accounts/${agencyId}/subscriptions/${subscriptionId}/cancel`, null);
  }
}
