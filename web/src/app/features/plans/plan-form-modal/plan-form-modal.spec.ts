import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { Plan } from '../plan.models';
import { PlanFormModal } from './plan-form-modal';

const URL = `${environment.apiUrl}/plans`;

const plan = (overrides: Partial<Plan> = {}): Plan => ({
  id: 'plan-1',
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

describe('PlanFormModal', () => {
  let http: HttpTestingController;

  const open = async (existing: Plan | null = null): Promise<ComponentFixture<PlanFormModal>> => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PlanFormModal);
    fixture.componentRef.setInput('plan', existing);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<PlanFormModal>, selector: string): T | null =>
    fixture.nativeElement.querySelector(selector) as T | null;

  const type = (fixture: ComponentFixture<PlanFormModal>, id: string, value: string): void => {
    const element = el<HTMLInputElement>(fixture, `#${id}`)!;
    element.value = value;
    element.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  const select = (fixture: ComponentFixture<PlanFormModal>, id: string, value: string): void => {
    const element = el<HTMLSelectElement>(fixture, `#${id}`)!;
    element.value = value;
    element.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const check = (fixture: ComponentFixture<PlanFormModal>, id: string): void => {
    el<HTMLInputElement>(fixture, `#${id}`)!.click();
    fixture.detectChanges();
  };

  const submit = (fixture: ComponentFixture<PlanFormModal>): void => {
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

  it('does not submit an empty form', async () => {
    const fixture = await open();

    submit(fixture);

    http.expectNone(URL);
    expect(el(fixture, '.text-error')).not.toBeNull();
  });

  it('creates a paid listings plan with its period and limit', async () => {
    const fixture = await open();
    type(fixture, 'plan-name', ' Inmobiliaria 200 ');
    type(fixture, 'plan-price', '1399');
    type(fixture, 'plan-limit', '200');
    select(fixture, 'plan-period', 'Month');

    submit(fixture);

    const request = http.expectOne(URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Inmobiliaria 200',
      description: null,
      kind: 'Listings',
      audience: 'Company',
      limit: 200,
      period: 'Month',
      price: 1399,
      currency: 'MXN',
      isActive: true,
    });
  });

  it('creates a featured package for any account with the chosen period', async () => {
    const fixture = await open();
    type(fixture, 'plan-name', 'Destacados semanales');
    type(fixture, 'plan-price', '199');
    select(fixture, 'plan-kind', 'Featured');
    select(fixture, 'plan-audience', 'Any');
    type(fixture, 'plan-limit', '3');
    select(fixture, 'plan-period', 'Week');

    submit(fixture);

    expect(http.expectOne(URL).request.body).toMatchObject({ kind: 'Featured', audience: 'Any', limit: 3, period: 'Week', price: 199 });
  });

  it('sends a null limit when the plan is unlimited and does not ask for a number', async () => {
    const fixture = await open();
    type(fixture, 'plan-name', 'Premium');
    type(fixture, 'plan-price', '4999');
    check(fixture, 'plan-unlimited');

    expect(el<HTMLInputElement>(fixture, '#plan-limit')!.disabled).toBe(true);
    submit(fixture);

    expect(http.expectOne(URL).request.body).toMatchObject({ limit: null, period: 'Month' });
  });

  it('turns a price of 0 into the free plan: individual, listings only, no period, always active', async () => {
    const fixture = await open();
    type(fixture, 'plan-name', 'Gratuito');
    type(fixture, 'plan-price', '0');
    type(fixture, 'plan-limit', '3');

    expect(el(fixture, '[data-testid="free-note"]')).not.toBeNull();
    expect(el(fixture, '#plan-period')).toBeNull();
    expect(el<HTMLSelectElement>(fixture, '#plan-kind')!.disabled).toBe(true);
    expect(el<HTMLSelectElement>(fixture, '#plan-audience')!.disabled).toBe(true);

    submit(fixture);

    expect(http.expectOne(URL).request.body).toMatchObject({
      kind: 'Listings',
      audience: 'Individual',
      limit: 3,
      period: null,
      price: 0,
      isActive: true,
    });
  });

  it('does not treat an empty price as free', async () => {
    const fixture = await open();

    expect(el(fixture, '[data-testid="free-note"]')).toBeNull();
    expect(el(fixture, '#plan-period')).not.toBeNull();
  });

  it('suggests the packages the portal sells for each kind', async () => {
    const fixture = await open();
    const options = () =>
      Array.from(fixture.nativeElement.querySelectorAll('#plan-limit-options option') as NodeListOf<HTMLOptionElement>).map((o) => o.value);

    expect(options()).toEqual(['3', '100', '200', '300', '500']);

    select(fixture, 'plan-kind', 'Featured');
    expect(options()).toEqual(['1', '3', '5', '10', '50', '100', '200', '300', '500', '1000']);
  });

  it('loads an existing plan and updates it with PUT', async () => {
    const fixture = await open(plan({ description: 'Hasta 100', limit: null }));

    expect(el<HTMLInputElement>(fixture, '#plan-name')!.value).toBe('Inmobiliaria 100');
    expect(el<HTMLInputElement>(fixture, '#plan-unlimited')!.checked).toBe(true);
    type(fixture, 'plan-price', '899');
    submit(fixture);

    const request = http.expectOne(`${URL}/plan-1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toMatchObject({ name: 'Inmobiliaria 100', description: 'Hasta 100', limit: null, price: 899 });
  });

  it('locks the price of the existing free plan', async () => {
    const fixture = await open(plan({ name: 'Gratuito', audience: 'Individual', limit: 3, period: null, price: 0, isFree: true }));

    expect(el<HTMLInputElement>(fixture, '#plan-price')!.disabled).toBe(true);
    submit(fixture);

    expect(http.expectOne(`${URL}/plan-1`).request.body).toMatchObject({ price: 0, period: null, kind: 'Listings', audience: 'Individual' });
  });

  it('says so when the name is already taken (409)', async () => {
    const fixture = await open();
    type(fixture, 'plan-name', 'Premium');
    type(fixture, 'plan-price', '10');
    type(fixture, 'plan-limit', '5');
    submit(fixture);

    http.expectOne(URL).flush({}, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ya existe un plan con ese nombre');
    expect(fixture.nativeElement.textContent).not.toContain('correo');
  });

  it('announces what was saved', async () => {
    const fixture = await open();
    const saved: string[] = [];
    fixture.componentInstance.saved.subscribe((m) => saved.push(m));
    type(fixture, 'plan-name', 'Premium');
    type(fixture, 'plan-price', '10');
    type(fixture, 'plan-limit', '5');
    submit(fixture);

    http.expectOne(URL).flush(plan());

    expect(saved).toEqual(['Plan creado correctamente.']);
  });
});
