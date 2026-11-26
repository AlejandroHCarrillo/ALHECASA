import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { AccountBilling } from '../../billing/billing.models';
import { SubscriptionList } from './subscription-list';

const ACCOUNTS = `${environment.apiUrl}/billing/accounts`;

const account = (overrides: Partial<AccountBilling> = {}): AccountBilling => ({
  agencyId: 'agency-1',
  agencyName: 'Inmobiliaria Demo',
  isIndividual: false,
  listings: { planName: 'Inmobiliaria 100', limit: 100, used: 40, endsAt: '2026-12-25T12:00:00Z', isFree: false },
  featured: { planName: '5 destacados', limit: 5, used: 2, endsAt: '2026-12-20T12:00:00Z' },
  ...overrides,
});

const individual = account({
  agencyId: 'agency-2',
  agencyName: 'Ana López',
  isIndividual: true,
  listings: { planName: 'Gratuito', limit: 3, used: 3, endsAt: null, isFree: true },
  featured: null,
});

const legacy = account({
  agencyId: 'agency-3',
  agencyName: 'Casas del Norte',
  listings: { planName: null, limit: null, used: 12, endsAt: null, isFree: false },
  featured: null,
});

describe('SubscriptionList', () => {
  let http: HttpTestingController;

  const open = (accounts: AccountBilling[] = [account(), individual, legacy]): ComponentFixture<SubscriptionList> => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SubscriptionList);
    fixture.detectChanges();
    http.expectOne(ACCOUNTS).flush(accounts);
    fixture.detectChanges();
    return fixture;
  };

  const rows = (fixture: ComponentFixture<SubscriptionList>) =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="account-row"]') as NodeListOf<HTMLElement>);

  beforeEach(() => {
    localStorage.clear();
    // jsdom no implementa <dialog>.showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });

  it('shows each account with its plan, usage, expiry and featured package', () => {
    const [company, person, noPlan] = rows(open()).map((r) => r.textContent as string);

    expect(company).toContain('Inmobiliaria');
    expect(company).toContain('Inmobiliaria 100');
    expect(company).toContain('40 de 100');
    expect(company).toContain('vence 25/12/2026');
    expect(company).toContain('5 destacados');
    expect(company).toContain('2 de 5');

    expect(person).toContain('Particular');
    expect(person).toContain('Gratuito');
    expect(person).toContain('3 de 3');
    expect(person).toContain('Sin paquete');

    expect(noPlan).toContain('Sin plan (sin tope)');
    expect(noPlan).toContain('12 de ilimitado');
  });

  it('flags an account that has reached its limit', () => {
    const fixture = open();
    const bars = rows(fixture).map((r) => r.querySelector('progress') as HTMLProgressElement | null);

    expect(bars[0]!.classList.contains('progress-error')).toBe(false);
    expect(bars[1]!.classList.contains('progress-error')).toBe(true);
    expect(bars[2]).toBeNull(); // sin tope no hay barra
  });

  it('searches by account name', () => {
    const fixture = open();
    const search = fixture.nativeElement.querySelector('#account-search') as HTMLInputElement;

    search.value = 'ana';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(rows(fixture)).toHaveLength(1);
    expect(rows(fixture)[0].textContent).toContain('Ana López');
  });

  it('opens the plans of the chosen account', () => {
    const fixture = open();

    (rows(fixture)[1].querySelector('[data-testid="open-plans"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-account-plans-modal')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Planes de Ana López');
    http.expectOne(`${environment.apiUrl}/plans`);
    http.expectOne(`${ACCOUNTS}/agency-2/subscriptions`);
  });

  it('shows the server error', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SubscriptionList);
    fixture.detectChanges();
    http.expectOne(ACCOUNTS).flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No tienes permiso');
    expect(fixture.nativeElement.textContent).toContain('No hay cuentas.');
  });
});
