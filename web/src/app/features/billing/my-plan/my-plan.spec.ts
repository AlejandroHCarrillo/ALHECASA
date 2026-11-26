import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { Plan } from '../../plans/plan.models';
import { AccountBilling } from '../billing.models';
import { MyPlan } from './my-plan';

const ME = `${environment.apiUrl}/billing/me`;
const PLANS = `${environment.apiUrl}/billing/plans`;
const CHECKOUT = `${environment.apiUrl}/billing/checkout`;

const plan = (overrides: Partial<Plan> = {}): Plan => ({
  id: 'plan-100',
  name: 'Inmobiliaria 100',
  description: 'Para empezar',
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

const PLANS_LIST = [
  plan(),
  plan({ id: 'plan-200', name: 'Inmobiliaria 200', limit: 200, price: 1399 }),
  plan({ id: 'feat-3', name: '3 destacados', kind: 'Featured', audience: 'Any', limit: 3, period: 'Week', price: 199 }),
];

const account = (overrides: Partial<AccountBilling> = {}): AccountBilling => ({
  agencyId: 'agency-1',
  agencyName: 'Demo',
  isIndividual: false,
  listings: { planName: 'Inmobiliaria 100', limit: 100, used: 40, endsAt: '2026-12-25T12:00:00Z', isFree: false },
  featured: null,
  ...overrides,
});

describe('MyPlan', () => {
  let http: HttpTestingController;

  const open = (acct: AccountBilling = account(), plans: Plan[] = PLANS_LIST): ComponentFixture<MyPlan> => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(MyPlan);
    fixture.detectChanges();
    http.expectOne(ME).flush(acct);
    http.expectOne(PLANS).flush(plans);
    fixture.detectChanges();
    return fixture;
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<MyPlan>, selector: string): T | null => fixture.nativeElement.querySelector(selector) as T | null;
  const cards = (fixture: ComponentFixture<MyPlan>) => Array.from(fixture.nativeElement.querySelectorAll('[data-testid="plan-card"]') as NodeListOf<HTMLElement>);

  beforeEach(() => {
    localStorage.clear();
    // jsdom no implementa <dialog>.showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });

  it('shows the current plan, its usage and expiry', () => {
    const fixture = open();

    expect(el(fixture, '[data-testid="listings-plan"]')!.textContent).toContain('Inmobiliaria 100');
    expect(el(fixture, '[data-testid="listings-usage"]')!.textContent).toContain('40 de 100');
    expect(el(fixture, '[data-testid="listings-usage"]')!.textContent).toContain('vence 25/12/2026');
    expect(el(fixture, '[data-testid="no-featured"]')).not.toBeNull();
  });

  it('shows the featured package when there is one', () => {
    const fixture = open(account({ featured: { planName: '3 destacados', limit: 3, used: 2, endsAt: '2026-12-01T12:00:00Z' } }));

    expect(el(fixture, '[data-testid="featured-plan"]')!.textContent).toContain('3 destacados');
    expect(el(fixture, '[data-testid="featured-usage"]')!.textContent).toContain('2 de 3');
  });

  it('shows the free plan of an individual account', () => {
    const fixture = open(account({ isIndividual: true, listings: { planName: 'Gratuito', limit: 3, used: 3, endsAt: null, isFree: true } }));

    expect(el(fixture, '[data-testid="listings-plan"]')!.textContent).toContain('Gratuito');
    expect(el(fixture, '[data-testid="listings-usage"]')!.textContent).toContain('3 de 3');
  });

  it('lists the listings plans first, with their limit and price', () => {
    const fixture = open();

    const texts = cards(fixture).map((c) => c.textContent as string);
    expect(texts).toHaveLength(2);
    expect(texts[0]).toContain('Inmobiliaria 100');
    expect(texts[0]).toContain('100 publicaciones');
    expect(texts[0]).toContain('MXN 799.00');
    expect(texts[0]).toContain('por mes');
    expect(texts[1]).toContain('MXN 1,399.00');
  });

  it('switches to the featured packages', () => {
    const fixture = open();

    el<HTMLButtonElement>(fixture, '[data-testid="tab-Featured"]')!.click();
    fixture.detectChanges();

    expect(cards(fixture)).toHaveLength(1);
    expect(cards(fixture)[0].textContent).toContain('3 destacados a la vez');
    expect(cards(fixture)[0].textContent).toContain('por semana');
  });

  it('marks the plan the account already has, and offers to renew it', () => {
    const fixture = open();
    const [current, other] = cards(fixture);

    expect(current.querySelector('[data-testid="current-badge"]')).not.toBeNull();
    expect(current.querySelector('[data-testid="buy"]')!.textContent).toContain('Renovar');
    expect(other.querySelector('[data-testid="current-badge"]')).toBeNull();
    expect(other.querySelector('[data-testid="buy"]')!.textContent).toContain('Comprar');
  });

  it('does not mark the free plan as a purchased one', () => {
    const fixture = open(account({ isIndividual: true, listings: { planName: 'Gratuito', limit: 3, used: 1, endsAt: null, isFree: true } }), [plan({ name: 'Gratuito' })]);

    expect(el(fixture, '[data-testid="current-badge"]')).toBeNull();
  });

  it('says so when there are no plans to buy', () => {
    const fixture = open(account(), []);

    expect(el(fixture, '[data-testid="no-plans"]')).not.toBeNull();
  });

  it('opens the checkout for the chosen plan, and closes it on cancel', () => {
    const fixture = open();

    (cards(fixture)[1].querySelector('[data-testid="buy"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture, 'app-checkout-modal')).not.toBeNull();
    expect(el(fixture, '[data-testid="order-total"]')!.textContent).toContain('MXN 1,399.00');

    const cancel = Array.from(fixture.nativeElement.querySelectorAll('.modal-action button') as NodeListOf<HTMLButtonElement>).find((b) => b.textContent?.includes('Cancelar'))!;
    cancel.click();
    fixture.detectChanges();

    expect(el(fixture, 'app-checkout-modal')).toBeNull();
  });

  it('after an approved payment closes the checkout and refreshes the plan', () => {
    const fixture = open();
    (cards(fixture)[1].querySelector('[data-testid="buy"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const type = (id: string, value: string) => {
      const input = el<HTMLInputElement>(fixture, `#${id}`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    type('card-number', '4242424242424242');
    type('card-holder', 'Ana López');
    type('card-expiry', '1299');
    type('card-cvv', '123');
    fixture.detectChanges();
    el<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));

    http.expectOne(CHECKOUT).flush({ id: 'sub-1' }, { status: 201, statusText: 'Created' });
    http.expectOne(ME).flush(account({ listings: { planName: 'Inmobiliaria 200', limit: 200, used: 40, endsAt: '2027-01-25T12:00:00Z', isFree: false } }));
    http.expectOne(PLANS).flush(PLANS_LIST);
    fixture.detectChanges();

    expect(el(fixture, 'app-checkout-modal')).toBeNull();
    expect(el(fixture, '[data-testid="listings-plan"]')!.textContent).toContain('Inmobiliaria 200');
  });

  it('shows the server error', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(MyPlan);
    fixture.detectChanges();
    http.expectOne(ME).flush({}, { status: 403, statusText: 'Forbidden' });
    http.expectOne(PLANS).flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No tienes permiso');
  });
});
