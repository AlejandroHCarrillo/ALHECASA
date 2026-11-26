import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { environment } from '../../../../environments/environment';
import { Plan } from '../plan.models';
import { PlanList } from './plan-list';

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

const free = plan({ id: 'free', name: 'Gratuito', audience: 'Individual', limit: 3, period: null, price: 0, isFree: true });
const featured = plan({ id: 'feat', name: '5 destacados', kind: 'Featured', audience: 'Any', limit: 5, price: 499 });

describe('PlanList', () => {
  let http: HttpTestingController;

  const open = (plans: Plan[] = [free, plan(), featured]): ComponentFixture<PlanList> => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PlanList);
    fixture.detectChanges();
    http.expectOne(URL).flush(plans);
    fixture.detectChanges();
    return fixture;
  };

  const rows = (fixture: ComponentFixture<PlanList>) => Array.from(fixture.nativeElement.querySelectorAll('[data-testid="plan-row"]') as NodeListOf<HTMLElement>);

  beforeEach(() => {
    localStorage.clear();
    // jsdom no implementa <dialog>.showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });

  it('lists every plan with what it includes, who it is for, its validity and price', () => {
    const fixture = open();
    const [freeRow, paidRow, featuredRow] = rows(fixture).map((r) => r.textContent as string);

    expect(rows(fixture)).toHaveLength(3);
    expect(freeRow).toContain('Gratuito');
    expect(freeRow).toContain('3 publicaciones');
    expect(freeRow).toContain('Particulares');
    expect(freeRow).toContain('Sin vencimiento');
    expect(freeRow).toContain('Gratis');
    expect(paidRow).toContain('100 publicaciones');
    expect(paidRow).toContain('Inmobiliarias');
    expect(paidRow).toContain('MXN 799.00');
    expect(paidRow).toContain('por mes');
    expect(featuredRow).toContain('5 destacados a la vez');
    expect(featuredRow).toContain('Cualquier cuenta');
  });

  it('tells the free plan apart and does not let it be deleted', () => {
    const fixture = open();
    const [freeRow, paidRow] = rows(fixture);

    expect(freeRow.querySelector('[data-testid="free-badge"]')).not.toBeNull();
    expect(paidRow.querySelector('[data-testid="free-badge"]')).toBeNull();
    expect((freeRow.querySelector('button[aria-label="Eliminar plan"]') as HTMLButtonElement).disabled).toBe(true);
    expect((paidRow.querySelector('button[aria-label="Eliminar plan"]') as HTMLButtonElement).disabled).toBe(false);
  });

  it('filters by kind without asking the server again', () => {
    const fixture = open();
    const select = fixture.nativeElement.querySelector('#plan-kind-filter') as HTMLSelectElement;

    select.value = 'Featured';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(rows(fixture)).toHaveLength(1);
    expect(rows(fixture)[0].textContent).toContain('5 destacados');
    http.expectNone(URL);
  });

  it('shows an empty message and the server error', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PlanList);
    fixture.detectChanges();
    http.expectOne(URL).flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No tienes permiso');
    expect(fixture.nativeElement.textContent).toContain('No hay planes.');
  });

  it('opens the form to add a plan', () => {
    const fixture = open();

    (fixture.nativeElement.querySelector('[data-testid="add-plan"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-plan-form-modal')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Agregar plan');
  });

  it('opens the form with the plan to edit', () => {
    const fixture = open();

    (rows(fixture)[1].querySelector('button[aria-label="Editar plan"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Editar plan');
    expect((fixture.nativeElement.querySelector('#plan-name') as HTMLInputElement).value).toBe('Inmobiliaria 100');
  });

  it('asks for confirmation, deletes the plan and reloads', () => {
    const fixture = open();

    (rows(fixture)[1].querySelector('button[aria-label="Eliminar plan"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('¿Seguro que deseas eliminar el plan');
    http.expectNone(`${URL}/plan-1`);

    const confirm = Array.from(fixture.nativeElement.querySelectorAll('.modal-action button') as NodeListOf<HTMLButtonElement>).find((b) => b.textContent?.includes('Eliminar'))!;
    confirm.click();

    const request = http.expectOne(`${URL}/plan-1`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(URL).flush([free, featured]);
    fixture.detectChanges();

    expect(rows(fixture)).toHaveLength(2);
  });
});
