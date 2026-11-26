import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { Plan } from '../../plans/plan.models';
import { AccountBilling } from '../billing.models';
import { CheckoutModal } from './checkout-modal';

const URL = `${environment.apiUrl}/billing/checkout`;

const plan = (overrides: Partial<Plan> = {}): Plan => ({
  id: 'plan-200',
  name: 'Inmobiliaria 200',
  description: null,
  kind: 'Listings',
  audience: 'Company',
  limit: 200,
  period: 'Month',
  price: 1399,
  currency: 'MXN',
  isActive: true,
  isFree: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
  ...overrides,
});

const account = (overrides: Partial<AccountBilling> = {}): AccountBilling => ({
  agencyId: 'agency-1',
  agencyName: 'Demo',
  isIndividual: false,
  listings: { planName: null, limit: null, used: 0, endsAt: null, isFree: false },
  featured: null,
  ...overrides,
});

describe('CheckoutModal', () => {
  let http: HttpTestingController;

  const open = async (p: Plan = plan(), a: AccountBilling = account()): Promise<ComponentFixture<CheckoutModal>> => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(CheckoutModal);
    fixture.componentRef.setInput('plan', p);
    fixture.componentRef.setInput('account', a);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<CheckoutModal>, selector: string): T | null =>
    fixture.nativeElement.querySelector(selector) as T | null;

  const type = (fixture: ComponentFixture<CheckoutModal>, id: string, value: string): void => {
    const element = el<HTMLInputElement>(fixture, `#${id}`)!;
    element.value = value;
    element.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  const fill = (fixture: ComponentFixture<CheckoutModal>, overrides: Partial<Record<'number' | 'holder' | 'expiry' | 'cvv', string>> = {}): void => {
    type(fixture, 'card-number', overrides.number ?? '4242424242424242');
    type(fixture, 'card-holder', overrides.holder ?? 'Ana López');
    type(fixture, 'card-expiry', overrides.expiry ?? '1299');
    type(fixture, 'card-cvv', overrides.cvv ?? '123');
  };

  const submit = (fixture: ComponentFixture<CheckoutModal>): void => {
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

  it('says the gateway is simulated and offers the test cards', async () => {
    const fixture = await open();

    expect(el(fixture, '[data-testid="simulated-note"]')!.textContent).toContain('simulada');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="test-card"]')).toHaveLength(3);
  });

  it('summarises the order with the plan and the total to pay', async () => {
    const fixture = await open();

    const summary = el(fixture, '[data-testid="order-summary"]')!.textContent as string;
    expect(summary).toContain('Inmobiliaria 200');
    expect(summary).toContain('200 publicaciones');
    expect(summary).toContain('un mes');
    expect(el(fixture, '[data-testid="order-total"]')!.textContent).toContain('MXN 1,399.00');
    expect(el(fixture, '[data-testid="pay"]')!.textContent).toContain('Pagar MXN 1,399.00');
  });

  it('formats the number and the expiry while typing', async () => {
    const fixture = await open();

    type(fixture, 'card-number', '4242424242424242');
    type(fixture, 'card-expiry', '1228');

    expect(el<HTMLInputElement>(fixture, '#card-number')!.value).toBe('4242 4242 4242 4242');
    expect(el<HTMLInputElement>(fixture, '#card-expiry')!.value).toBe('12/28');
  });

  it('does not pay with an empty form', async () => {
    const fixture = await open();

    submit(fixture);

    http.expectNone(URL);
    expect(fixture.nativeElement.textContent).toContain('Captura el número de tarjeta.');
  });

  it.each([
    [{ number: '4242424242424241' }, 'no es válido'],
    [{ expiry: '1320' }, 'MM/AA'],
    [{ expiry: '0120' }, 'vencida'],
    [{ cvv: '12' }, '3 o 4 dígitos'],
    [{ holder: '  ' }, 'nombre'],
  ])('rejects %j without calling the API', async (overrides, message) => {
    const fixture = await open();
    fill(fixture, overrides);

    submit(fixture);

    http.expectNone(URL);
    expect(fixture.nativeElement.textContent).toContain(message);
  });

  it('sends only the plan and the card, with the digits and a four-digit year', async () => {
    const fixture = await open();
    fill(fixture, { number: '4242 4242 4242 4242', expiry: '1228' });

    submit(fixture);

    const request = http.expectOne(URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      planId: 'plan-200',
      card: { number: '4242424242424242', holderName: 'Ana López', expiryMonth: 12, expiryYear: 2028, cvv: '123' },
    });
  });

  it('announces the purchase when the payment is approved', async () => {
    const fixture = await open();
    const purchased: string[] = [];
    fixture.componentInstance.purchased.subscribe((m) => purchased.push(m));
    fill(fixture);
    submit(fixture);

    http.expectOne(URL).flush({ id: 'sub-1' }, { status: 201, statusText: 'Created' });

    expect(purchased).toEqual(['Pago aprobado. Ya tienes el plan «Inmobiliaria 200».']);
  });

  it('shows the reason when the card is declined, and lets the user try again', async () => {
    const fixture = await open();
    const purchased = vi.fn();
    fixture.componentInstance.purchased.subscribe(purchased);
    fill(fixture);
    submit(fixture);

    http.expectOne(URL).flush({ detail: 'La tarjeta no tiene fondos suficientes.' }, { status: 402, statusText: 'Payment Required' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('La tarjeta no tiene fondos suficientes.');
    expect(purchased).not.toHaveBeenCalled();
    expect(el<HTMLButtonElement>(fixture, '[data-testid="pay"]')!.disabled).toBe(false);
  });

  it('fills the form from a test card', async () => {
    const fixture = await open();

    el<HTMLButtonElement>(fixture, '[data-testid="test-card"]')!.click();
    fixture.detectChanges();
    submit(fixture);

    const body = http.expectOne(URL).request.body as { card: { number: string } };
    expect(body.card.number).toBe('4242424242424242');
  });

  it('warns that the new listings plan replaces the current one', async () => {
    const fixture = await open(plan(), account({ listings: { planName: 'Inmobiliaria 100', limit: 100, used: 5, endsAt: '2026-12-01T12:00:00Z', isFree: false } }));

    expect(el(fixture, '[data-testid="replace-warning"]')!.textContent).toContain('Reemplaza a tu plan actual «Inmobiliaria 100»');
  });

  it('does not warn when the account only has the free plan', async () => {
    const fixture = await open(plan(), account({ isIndividual: true, listings: { planName: 'Gratuito', limit: 3, used: 1, endsAt: null, isFree: true } }));

    expect(el(fixture, '[data-testid="replace-warning"]')).toBeNull();
  });

  it('warns that a featured package clears the featured ads', async () => {
    const featured = plan({ id: 'f', name: '5 destacados', kind: 'Featured', audience: 'Any', limit: 5, period: 'Week', price: 499 });
    const fixture = await open(featured, account({ featured: { planName: '3 destacados', limit: 3, used: 2, endsAt: '2026-12-01T12:00:00Z' } }));

    const warning = el(fixture, '[data-testid="replace-warning"]')!.textContent as string;
    expect(warning).toContain('Reemplaza a tu plan actual «3 destacados»');
    expect(warning).toContain('dejarán de estarlo');
  });

  it('can be cancelled', async () => {
    const fixture = await open();
    const cancelled = vi.fn();
    fixture.componentInstance.cancelled.subscribe(cancelled);

    const cancel = Array.from(fixture.nativeElement.querySelectorAll('.modal-action button') as NodeListOf<HTMLButtonElement>).find((b) => b.textContent?.includes('Cancelar'))!;
    cancel.click();

    expect(cancelled).toHaveBeenCalledOnce();
  });
});
