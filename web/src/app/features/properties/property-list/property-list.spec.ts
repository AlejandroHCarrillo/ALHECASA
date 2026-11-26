import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/models/user-role';
import { AccountBilling } from '../../billing/billing.models';
import { Property } from '../property.models';
import { PropertyList } from './property-list';

const URL = `${environment.apiUrl}/properties`;

const property = (overrides: Partial<Property> = {}): Property => ({
  id: 'p-1',
  agencyId: 'agency-1',
  agencyName: 'Demo',
  title: 'Casa en Polanco',
  description: null,
  type: 'House',
  operation: 'Sale',
  status: 'Published',
  price: 4_500_000,
  currency: 'MXN',
  street: null,
  neighborhood: null,
  city: 'Ciudad de México',
  state: 'CDMX',
  postalCode: null,
  bedrooms: 3,
  bathrooms: 2,
  constructionArea: 250,
  landArea: 300,
  latitude: null,
  longitude: null,
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  isFeatured: false,
  featuredUntil: null,
  imageIds: [],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
  ...overrides,
});

const page = (items: Property[], total = items.length) => ({ items, page: 1, pageSize: 10, totalCount: total, totalPages: Math.ceil(total / 10) });

describe('PropertyList: filters', () => {
  let http: HttpTestingController;

  const open = (): ComponentFixture<PropertyList> => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Me', email: 'me@a.com', role: UserRole.Seller, agencyId: 'agency-1', agencyName: null },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PropertyList);
    fixture.detectChanges();
    return fixture;
  };

  const flush = (fixture: ComponentFixture<PropertyList>, items: Property[] = [], total = items.length) => {
    const request = http.expectOne((r) => r.url === URL);
    request.flush(page(items, total));
    fixture.detectChanges();
    return request.request;
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<PropertyList>, selector: string): T => fixture.nativeElement.querySelector(selector) as T;

  const choose = (fixture: ComponentFixture<PropertyList>, id: string, value: string): void => {
    const element = el<HTMLSelectElement | HTMLInputElement>(fixture, `#${id}`);
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
    fixture.detectChanges();
  };

  const click = (fixture: ComponentFixture<PropertyList>, selector: string): void => {
    el(fixture, selector).click();
    fixture.detectChanges();
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('asks for the first page with no filters', () => {
    const fixture = open();
    const request = flush(fixture, [property()]);

    for (const name of ['search', 'status', 'type', 'operation', 'minPrice', 'currency', 'amenities', 'sort']) {
      expect(request.params.has(name)).toBe(false);
    }
    expect(request.params.get('page')).toBe('1');
    expect(el(fixture, '[data-testid="clear-filters"]')).toBeNull();
  });

  it('offers every status, type and operation', () => {
    const fixture = open();
    flush(fixture);

    const options = (id: string) => Array.from(fixture.nativeElement.querySelectorAll(`#${id} option`) as NodeListOf<HTMLOptionElement>).map((o) => o.value);
    expect(options('list-status')).toEqual(['', 'Draft', 'Published', 'Paused', 'Closed']);
    expect(options('list-type')).toEqual(['', 'House', 'Apartment', 'Land', 'Commercial', 'Office']);
    expect(options('list-operation')).toEqual(['', 'Sale', 'Rent']);
  });

  it('filters by status, type and operation as soon as they are chosen', () => {
    const fixture = open();
    flush(fixture);

    choose(fixture, 'list-status', 'Draft');
    expect(http.expectOne((r) => r.url === URL).request.params.get('status')).toBe('Draft');
    http.match((r) => r.url === URL).forEach((r) => r.flush(page([])));

    choose(fixture, 'list-type', 'Apartment');
    choose(fixture, 'list-operation', 'Rent');
    const requests = http.match((r) => r.url === URL);
    const last = requests.filter((r) => !r.cancelled).at(-1)!.request;
    expect(last.params.get('status')).toBe('Draft');
    expect(last.params.get('type')).toBe('Apartment');
    expect(last.params.get('operation')).toBe('Rent');
    requests.filter((r) => !r.cancelled).forEach((r) => r.flush(page([])));
  });

  it('applies the advanced filters only when «Aplicar filtros» is pressed, with price and sort', () => {
    const fixture = open();
    flush(fixture);

    click(fixture, '[data-testid="toggle-advanced"]');
    choose(fixture, 'list-minPrice', '1000000');
    choose(fixture, 'list-currency', 'MXN');
    click(fixture, '#list-amenity-Pool');
    choose(fixture, 'list-sort', 'PriceAsc');
    http.expectNone((r) => r.url === URL);

    el<HTMLFormElement>(fixture, 'form[role="search"]').dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const request = http.expectOne((r) => r.url === URL).request;
    expect(request.params.get('minPrice')).toBe('1000000');
    expect(request.params.get('currency')).toBe('MXN');
    expect(request.params.getAll('amenities')).toEqual(['Pool']);
    expect(request.params.get('sort')).toBe('PriceAsc');
    expect(request.params.get('page')).toBe('1');
    expect(el(fixture, '[data-testid="advanced-count"]').textContent).toContain('2');
  });

  it('keeps the applied advanced filters when a select changes afterwards', () => {
    const fixture = open();
    flush(fixture);
    click(fixture, '[data-testid="toggle-advanced"]');
    click(fixture, '#list-amenity-Gym');
    el<HTMLFormElement>(fixture, 'form[role="search"]').dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    http.expectOne((r) => r.url === URL).flush(page([]));

    choose(fixture, 'list-status', 'Paused');

    const request = http.expectOne((r) => r.url === URL).request;
    expect(request.params.get('status')).toBe('Paused');
    expect(request.params.getAll('amenities')).toEqual(['Gym']);
  });

  it('clears every filter at once', () => {
    const fixture = open();
    flush(fixture);
    choose(fixture, 'list-status', 'Draft');
    http.expectOne((r) => r.url === URL).flush(page([]));
    fixture.detectChanges();

    click(fixture, '[data-testid="clear-filters"]');

    const request = http.expectOne((r) => r.url === URL).request;
    expect(request.params.has('status')).toBe(false);
    expect(el<HTMLSelectElement>(fixture, '#list-status').value).toBe('');
    expect(el(fixture, '[data-testid="clear-filters"]')).toBeNull();
  });
});

describe('PropertyList: plan and featured ads', () => {
  const BILLING = `${environment.apiUrl}/billing/me`;
  let http: HttpTestingController;

  const billing = (overrides: Partial<AccountBilling> = {}): AccountBilling => ({
    agencyId: 'agency-1',
    agencyName: 'Demo',
    isIndividual: false,
    listings: { planName: 'Inmobiliaria 100', limit: 100, used: 40, endsAt: '2026-12-25T12:00:00Z', isFree: false },
    featured: { planName: '5 destacados', limit: 5, used: 2, endsAt: '2026-12-20T12:00:00Z' },
    ...overrides,
  });

  const open = (role: UserRole = UserRole.Seller): ComponentFixture<PropertyList> => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Me', email: 'me@a.com', role, agencyId: role === UserRole.PlatformAdmin ? null : 'agency-1', agencyName: null },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PropertyList);
    fixture.detectChanges();
    return fixture;
  };

  const flushList = (fixture: ComponentFixture<PropertyList>, items: Property[]) => {
    http.expectOne((r) => r.url === URL).flush(page(items));
    fixture.detectChanges();
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<PropertyList>, selector: string): T | null => fixture.nativeElement.querySelector(selector) as T | null;

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('summarises the account plan: listings used and the featured package', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(billing());
    flushList(fixture, [property()]);

    expect(el(fixture, '[data-testid="listings-usage"]')!.textContent).toContain('40 de 100');
    expect(el(fixture, '[data-testid="listings-usage"]')!.textContent).toContain('vence 25/12/2026');
    expect(el(fixture, '[data-testid="featured-usage"]')!.textContent).toContain('2 de 5');
    expect(el(fixture, '[data-testid="my-plan"]')!.textContent).toContain('Inmobiliaria 100');
  });

  it('offers the account administrator a link to buy or change plan, and nobody else', () => {
    const admin = open(UserRole.AgencyAdmin);
    http.expectOne(BILLING).flush(billing());
    flushList(admin, []);
    expect(el<HTMLAnchorElement>(admin, '[data-testid="buy-plan"]')!.getAttribute('href')).toBe('/mi-plan');

    TestBed.resetTestingModule();
    const seller = open(UserRole.Seller);
    http.expectOne(BILLING).flush(billing());
    flushList(seller, []);
    expect(el(seller, '[data-testid="buy-plan"]')).toBeNull();
  });

  it('shows the free plan of an individual and that there is no featured package', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(
      billing({ isIndividual: true, listings: { planName: 'Gratuito', limit: 3, used: 3, endsAt: null, isFree: true }, featured: null }),
    );
    flushList(fixture, []);

    expect(el(fixture, '[data-testid="my-plan"]')!.textContent).toContain('Gratuito');
    expect(el(fixture, '[data-testid="listings-usage"]')!.textContent).toContain('3 de 3');
    expect(el(fixture, '[data-testid="no-featured"]')).not.toBeNull();
  });

  it('keeps the list working when the plan summary cannot be loaded', () => {
    const fixture = open();
    http.expectOne(BILLING).flush({}, { status: 500, statusText: 'Server Error' });
    flushList(fixture, [property()]);

    expect(el(fixture, '[data-testid="my-plan"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Casa en Polanco');
  });

  it('does not ask for a plan summary from the platform administrator, who has no account', () => {
    const fixture = open(UserRole.PlatformAdmin);
    http.match((r) => r.url === `${environment.apiUrl}/agencies`).forEach((r) => r.flush([]));
    flushList(fixture, [property()]);

    http.expectNone(BILLING);
    expect(el(fixture, '[data-testid="my-plan"]')).toBeNull();
  });

  it('badges the featured properties', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(billing());
    flushList(fixture, [property({ id: 'p-1', isFeatured: true }), property({ id: 'p-2', title: 'Otra', isFeatured: false })]);

    expect(fixture.nativeElement.querySelectorAll('[data-testid="featured-badge"]')).toHaveLength(1);
  });

  it('offers the star only for published properties, or for the ones already featured', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(billing());
    flushList(fixture, [
      property({ id: 'p-pub', status: 'Published' }),
      property({ id: 'p-draft', title: 'Borrador', status: 'Draft' }),
      property({ id: 'p-old', title: 'Pausada', status: 'Paused', isFeatured: true }),
    ]);

    expect(fixture.nativeElement.querySelectorAll('[data-testid="toggle-featured"]')).toHaveLength(2);
  });

  it('features a property, then refreshes the list and the plan summary', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(billing());
    flushList(fixture, [property({ id: 'p-1' })]);

    el<HTMLButtonElement>(fixture, '[data-testid="toggle-featured"]')!.click();

    const request = http.expectOne(`${URL}/p-1/featured`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ featured: true });
    request.flush(property({ id: 'p-1', isFeatured: true, featuredUntil: '2026-12-20T12:00:00Z' }));
    http.expectOne(BILLING).flush(billing({ featured: { planName: '5 destacados', limit: 5, used: 3, endsAt: '2026-12-20T12:00:00Z' } }));
    flushList(fixture, [property({ id: 'p-1', isFeatured: true })]);

    expect(el(fixture, '[data-testid="featured-usage"]')!.textContent).toContain('3 de 5');
    expect(el(fixture, '[data-testid="featured-badge"]')).not.toBeNull();
  });

  it('removes the featured mark with the same button', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(billing());
    flushList(fixture, [property({ id: 'p-1', isFeatured: true })]);

    el<HTMLButtonElement>(fixture, '[data-testid="toggle-featured"]')!.click();

    expect(http.expectOne(`${URL}/p-1/featured`).request.body).toEqual({ featured: false });
  });

  it('shows the plan message when the package has no free slot (422)', () => {
    const fixture = open();
    http.expectOne(BILLING).flush(billing());
    flushList(fixture, [property({ id: 'p-1' })]);

    el<HTMLButtonElement>(fixture, '[data-testid="toggle-featured"]')!.click();
    http.expectOne(`${URL}/p-1/featured`).flush(
      { detail: 'Tu paquete "5 destacados" permite 5 anuncios destacados a la vez.' },
      { status: 422, statusText: 'Unprocessable' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('permite 5 anuncios destacados a la vez');
    expect(el<HTMLButtonElement>(fixture, '[data-testid="toggle-featured"]')!.disabled).toBe(false);
  });
});

describe('PropertyList: opened from the home cards', () => {
  let http: HttpTestingController;

  const open = (status: string): ComponentFixture<PropertyList> => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Me', email: 'me@a.com', role: UserRole.Seller, agencyId: 'agency-1', agencyName: null },
      }),
    );
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ status }) } } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PropertyList);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it.each(['Published', 'Draft', 'Paused', 'Closed'])('opens already filtered by ?status=%s', (status) => {
    open(status);

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.params.get('status')).toBe(status);
    request.flush(page([]));
  });

  it('ignores a ?status= that is not a status', () => {
    open('Hackeada');

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.params.has('status')).toBe(false);
    request.flush(page([]));
  });
});
