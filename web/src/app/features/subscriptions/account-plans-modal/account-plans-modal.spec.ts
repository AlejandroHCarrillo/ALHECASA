import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { AccountBilling, Subscription } from '../../billing/billing.models';
import { Plan } from '../../plans/plan.models';
import { AccountPlansModal } from './account-plans-modal';

const PLANS = `${environment.apiUrl}/plans`;
const SUBSCRIPTIONS = `${environment.apiUrl}/billing/accounts/agency-1/subscriptions`;

const plan = (overrides: Partial<Plan> = {}): Plan => ({
  id: 'plan-100',
  name: 'Inmobiliaria 100',
  description: null,
  kind: 'Listings',
  audience: 'Company',
  limit: 100,
  period: 'Month',
  price: 799,
  currency: 'MXN',
  isActive: true,
  isFree: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
  ...overrides,
});

const PLANS_LIST: Plan[] = [
  plan({ id: 'free', name: 'Gratuito', audience: 'Individual', limit: 3, period: null, price: 0, isFree: true }),
  plan(),
  plan({ id: 'plan-feat', name: '5 destacados', kind: 'Featured', audience: 'Any', limit: 5, period: 'Week', price: 199 }),
  plan({ id: 'plan-person', name: 'Particular Plus', audience: 'Individual', limit: 10, price: 99 }),
  plan({ id: 'plan-off', name: 'Retirado', isActive: false }),
];

const subscription = (overrides: Partial<Subscription> = {}): Subscription => ({
  id: 'sub-1',
  agencyId: 'agency-1',
  planId: 'plan-100',
  planName: 'Inmobiliaria 100',
  kind: 'Listings',
  limit: 100,
  period: 'Month',
  amountPaid: 799,
  currency: 'MXN',
  paymentReference: 'SPEI-123',
  startsAt: '2026-11-01T00:00:00Z',
  endsAt: '2026-12-01T00:00:00Z',
  cancelledAt: null,
  isActive: true,
  createdAt: '2026-11-01T00:00:00Z',
  ...overrides,
});

const account = (overrides: Partial<AccountBilling> = {}): AccountBilling => ({
  agencyId: 'agency-1',
  agencyName: 'Inmobiliaria Demo',
  isIndividual: false,
  listings: { planName: null, limit: null, used: 0, endsAt: null, isFree: false },
  featured: null,
  ...overrides,
});

describe('AccountPlansModal', () => {
  let http: HttpTestingController;

  const open = async (
    history: Subscription[] = [],
    acct: AccountBilling = account(),
  ): Promise<ComponentFixture<AccountPlansModal>> => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(AccountPlansModal);
    fixture.componentRef.setInput('account', acct);
    fixture.detectChanges();
    http.expectOne(PLANS).flush(PLANS_LIST);
    http.expectOne(`${environment.apiUrl}/billing/accounts/${acct.agencyId}/subscriptions`).flush(history);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<AccountPlansModal>, selector: string): T | null =>
    fixture.nativeElement.querySelector(selector) as T | null;

  const planOptions = (fixture: ComponentFixture<AccountPlansModal>): string[] =>
    Array.from(fixture.nativeElement.querySelectorAll('#assign-plan option') as NodeListOf<HTMLOptionElement>)
      .map((o) => o.value)
      .filter(Boolean);

  const choosePlan = (fixture: ComponentFixture<AccountPlansModal>, id: string): void => {
    const select = el<HTMLSelectElement>(fixture, '#assign-plan')!;
    select.value = id;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const submit = (fixture: ComponentFixture<AccountPlansModal>): void => {
    el<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  beforeEach(() => {
    localStorage.clear();
    // jsdom no implementa <dialog>.showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });

  it('offers a company only the active paid plans meant for companies or anyone', async () => {
    const fixture = await open();

    expect(planOptions(fixture)).toEqual(['plan-100', 'plan-feat']);
  });

  it('offers an individual account the plans meant for individuals or anyone, never the free one', async () => {
    const fixture = await open([], account({ isIndividual: true }));

    expect(planOptions(fixture)).toEqual(['plan-person', 'plan-feat']);
  });

  it('explains what an account with nothing contracted has', async () => {
    expect((await open()).nativeElement.textContent).toContain('no tiene tope de publicaciones');
    TestBed.resetTestingModule();
    expect((await open([], account({ isIndividual: true }))).nativeElement.textContent).toContain('plan gratuito');
  });

  it('shows the active subscription and the history with its status', async () => {
    const fixture = await open([
      subscription(),
      subscription({ id: 'sub-2', planName: 'Anterior', isActive: false, cancelledAt: '2026-10-15T00:00:00Z' }),
      subscription({ id: 'sub-3', planName: 'Vieja', isActive: false }),
    ]);

    expect(el(fixture, '[data-testid="active-subscription"]')!.textContent).toContain('Inmobiliaria 100');
    const rows = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="history-row"]') as NodeListOf<HTMLElement>).map((r) => r.textContent as string);
    expect(rows).toHaveLength(3);
    expect(rows[0]).toContain('Vigente');
    expect(rows[0]).toContain('SPEI-123');
    expect(rows[1]).toContain('Cancelada');
    expect(rows[2]).toContain('Vencida');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="cancel-subscription"]')).toHaveLength(1);
  });

  it('prefills the amount with the plan price and shows how long it lasts', async () => {
    const fixture = await open();

    choosePlan(fixture, 'plan-feat');

    expect(el<HTMLInputElement>(fixture, '#assign-amount')!.value).toBe('199');
    expect(el(fixture, '[data-testid="assign-summary"]')!.textContent).toContain('una semana');
  });

  it('requires choosing a plan', async () => {
    const fixture = await open();

    submit(fixture);

    http.expectNone(SUBSCRIPTIONS);
    expect(fixture.nativeElement.textContent).toContain('Selecciona un plan.');
  });

  it('registers the contract with the amount and payment reference, then refreshes', async () => {
    const fixture = await open();
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    choosePlan(fixture, 'plan-100');
    const amount = el<HTMLInputElement>(fixture, '#assign-amount')!;
    amount.value = '700';
    amount.dispatchEvent(new Event('input'));
    const reference = el<HTMLInputElement>(fixture, '#assign-reference')!;
    reference.value = ' SPEI-9 ';
    reference.dispatchEvent(new Event('input'));

    submit(fixture);

    const request = http.expectOne(SUBSCRIPTIONS);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ planId: 'plan-100', amountPaid: 700, paymentReference: 'SPEI-9' });
    request.flush(subscription());
    http.expectOne(SUBSCRIPTIONS).flush([subscription()]);
    fixture.detectChanges();

    expect(changed).toHaveBeenCalledOnce();
    expect(el(fixture, '[data-testid="active-subscription"]')).not.toBeNull();
    expect(el<HTMLSelectElement>(fixture, '#assign-plan')!.value).toBe('');
  });

  it('warns that a new package replaces the current one and clears the featured ads', async () => {
    const fixture = await open([subscription({ id: 'sub-f', kind: 'Featured', planName: '3 destacados', limit: 3 })]);

    choosePlan(fixture, 'plan-feat');

    const warning = el(fixture, '[data-testid="replace-warning"]')!.textContent as string;
    expect(warning).toContain('Reemplaza a «3 destacados»');
    expect(warning).toContain('dejarán de estarlo');
  });

  it('does not warn when the active one is of another kind', async () => {
    const fixture = await open([subscription()]);

    choosePlan(fixture, 'plan-feat');

    expect(el(fixture, '[data-testid="replace-warning"]')).toBeNull();
  });

  it('shows the server error when the plan cannot be assigned', async () => {
    const fixture = await open();
    choosePlan(fixture, 'plan-100');
    submit(fixture);

    http.expectOne(SUBSCRIPTIONS).flush({}, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Los datos enviados no son válidos.');
  });

  it('asks before cancelling and cancels only after confirming', async () => {
    const fixture = await open([subscription()]);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);

    el<HTMLButtonElement>(fixture, '[data-testid="cancel-subscription"]')!.click();
    fixture.detectChanges();
    expect(el(fixture, '[data-testid="cancel-confirm"]')).not.toBeNull();
    http.expectNone(`${SUBSCRIPTIONS}/sub-1/cancel`);

    el<HTMLButtonElement>(fixture, '[data-testid="cancel-yes"]')!.click();

    const request = http.expectOne(`${SUBSCRIPTIONS}/sub-1/cancel`);
    expect(request.request.method).toBe('POST');
    request.flush(subscription({ isActive: false, cancelledAt: '2026-11-25T00:00:00Z' }));
    http.expectOne(SUBSCRIPTIONS).flush([subscription({ isActive: false, cancelledAt: '2026-11-25T00:00:00Z' })]);
    fixture.detectChanges();

    expect(changed).toHaveBeenCalledOnce();
    expect(el(fixture, '[data-testid="cancel-confirm"]')).toBeNull();
    expect(el(fixture, '[data-testid="no-active"]')).not.toBeNull();
  });
});
