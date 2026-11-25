import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/models/user-role';
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
