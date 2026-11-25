import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Landing } from './landing';
import { PublicProperty } from './public-properties.service';

const URL = `${environment.apiUrl}/public/properties`;

const sample = (overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id: 'p-1',
  title: 'Casa moderna con alberca',
  description: null,
  type: 'House',
  operation: 'Sale',
  price: 18_500_000,
  currency: 'MXN',
  neighborhood: 'Lomas',
  city: 'Ciudad de México',
  state: 'CDMX',
  bedrooms: 4,
  bathrooms: 5,
  constructionArea: 520,
  landArea: 600,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: [],
  agencyName: 'Demo Real Estate',
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  ...overrides,
});

const page = (items: PublicProperty[], total = items.length, pageNumber = 1) => ({
  items,
  page: pageNumber,
  pageSize: 12,
  totalCount: total,
  totalPages: Math.ceil(total / 12),
});

describe('Landing: advanced filters, sort and the URL', () => {
  let http: HttpTestingController;
  let params$: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let navigate: ReturnType<typeof vi.spyOn>;

  /** Abre la portada como si la dirección trajera estos parámetros. */
  const open = (query: Record<string, string> = {}): ComponentFixture<Landing> => {
    params$ = new BehaviorSubject(convertToParamMap(query));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { queryParamMap: params$.asObservable() } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(Landing);
    fixture.detectChanges();
    return fixture;
  };

  const flush = (fixture: ComponentFixture<Landing>, items: PublicProperty[] = [], total = items.length) => {
    const request = http.expectOne((r) => r.url === URL);
    request.flush(page(items, total));
    fixture.detectChanges();
    return request.request;
  };

  const el = <T extends HTMLElement>(fixture: ComponentFixture<Landing>, selector: string): T =>
    fixture.nativeElement.querySelector(selector) as T;

  const type = (fixture: ComponentFixture<Landing>, id: string, value: string): void => {
    const element = el<HTMLInputElement | HTMLSelectElement>(fixture, `#${id}`);
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  };

  const click = (fixture: ComponentFixture<Landing>, selector: string): void => {
    el(fixture, selector).click();
    fixture.detectChanges();
  };

  const openPanel = (fixture: ComponentFixture<Landing>): void => click(fixture, '[data-testid="toggle-advanced"]');

  const search = (fixture: ComponentFixture<Landing>): void => {
    el<HTMLFormElement>(fixture, 'form[role="search"]').dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('starts with the advanced panel closed and no count', () => {
    const fixture = open();
    flush(fixture);

    expect(el(fixture, '#advanced-panel')).toBeNull();
    expect(el(fixture, '[data-testid="toggle-advanced"]').getAttribute('aria-expanded')).toBe('false');
    expect(el(fixture, '[data-testid="advanced-count"]')).toBeNull();
  });

  it('opens and closes the panel, keeping what was typed', () => {
    const fixture = open();
    flush(fixture);

    openPanel(fixture);
    expect(el(fixture, '#advanced-panel')).not.toBeNull();
    expect(el(fixture, '[data-testid="toggle-advanced"]').getAttribute('aria-expanded')).toBe('true');
    type(fixture, 'landing-minBedrooms', '3');

    openPanel(fixture);
    expect(el(fixture, '#advanced-panel')).toBeNull();
    openPanel(fixture);
    expect(el<HTMLInputElement>(fixture, '#landing-minBedrooms').value).toBe('3');
  });

  it('sends every advanced filter on Buscar, and counts them in the button', () => {
    const fixture = open();
    flush(fixture);
    openPanel(fixture);

    type(fixture, 'landing-minPrice', '1000000');
    type(fixture, 'landing-maxPrice', '5000000');
    type(fixture, 'landing-currency', 'USD');
    type(fixture, 'landing-minBedrooms', '3');
    type(fixture, 'landing-minBathrooms', '2');
    type(fixture, 'landing-minParking', '1');
    type(fixture, 'landing-minArea', '80');
    type(fixture, 'landing-maxArea', '300');
    click(fixture, '#landing-petsAllowed');
    click(fixture, '#landing-furnished');
    click(fixture, '#landing-amenity-Pool');
    click(fixture, '#landing-amenity-Gym');
    type(fixture, 'landing-sort', 'PriceAsc');
    fixture.detectChanges();
    http.expectNone((r) => r.url === URL);

    search(fixture);

    const request = http.expectOne((r) => r.url === URL).request;
    expect(request.params.get('minPrice')).toBe('1000000');
    expect(request.params.get('maxPrice')).toBe('5000000');
    expect(request.params.get('currency')).toBe('USD');
    expect(request.params.get('minBedrooms')).toBe('3');
    expect(request.params.get('minBathrooms')).toBe('2');
    expect(request.params.get('minParking')).toBe('1');
    expect(request.params.get('minArea')).toBe('80');
    expect(request.params.get('maxArea')).toBe('300');
    expect(request.params.get('petsAllowed')).toBe('true');
    expect(request.params.get('furnished')).toBe('true');
    expect(request.params.getAll('amenities')).toEqual(['Pool', 'Gym']);
    expect(request.params.get('sort')).toBe('PriceAsc');
    expect(request.params.get('page')).toBe('1');

    // precio (1) + recámaras + baños + estacionamientos + superficie (1) + mascotas + amueblada + 2 amenidades = 9
    expect(el(fixture, '[data-testid="advanced-count"]').textContent).toContain('9');
  });

  it('sends nothing extra when no advanced filter is used (and no currency without a price)', () => {
    const fixture = open();
    const request = flush(fixture);

    for (const name of ['minPrice', 'maxPrice', 'currency', 'minBedrooms', 'petsAllowed', 'furnished', 'amenities', 'sort']) {
      expect(request.params.has(name)).toBe(false);
    }
  });

  it('describes the chosen sort in the heading, and offers to clear it', () => {
    const fixture = open();
    flush(fixture, [sample()]);
    expect(fixture.nativeElement.textContent).toContain('Las más recientes primero.');
    expect(fixture.nativeElement.textContent).not.toContain('Quitar filtros');

    openPanel(fixture);
    type(fixture, 'landing-sort', 'PriceDesc');
    search(fixture);
    flush(fixture, [sample()]);

    expect(fixture.nativeElement.textContent).toContain('Del precio más alto al más bajo.');
    expect(fixture.nativeElement.textContent).toContain('Quitar filtros');
  });

  it('clears the advanced filters too', () => {
    const fixture = open();
    flush(fixture);
    openPanel(fixture);
    type(fixture, 'landing-minBedrooms', '3');
    click(fixture, '#landing-amenity-Pool');
    search(fixture);
    flush(fixture, [sample()]);
    expect(el(fixture, '[data-testid="advanced-count"]').textContent).toContain('2');

    click(fixture, '#advanced-panel button.btn-ghost'); // «Limpiar»

    const request = http.expectOne((r) => r.url === URL).request;
    expect(request.params.has('minBedrooms')).toBe(false);
    expect(request.params.has('amenities')).toBe(false);
    expect(el(fixture, '[data-testid="advanced-count"]')).toBeNull();
    expect(el<HTMLInputElement>(fixture, '#landing-minBedrooms').value).toBe('');
  });

  it('tells the visitor when the advanced filters leave no results', () => {
    const fixture = open();
    flush(fixture);
    openPanel(fixture);
    click(fixture, '#landing-amenity-Pool');
    search(fixture);
    flush(fixture, []);

    expect(fixture.nativeElement.textContent).toContain('No hay propiedades que coincidan con los filtros.');
  });

  // --- La dirección (URL) -----------------------------------------------------------------

  it('writes the applied filters in the address, replacing the history entry, with only the ones that have a value', () => {
    const fixture = open();
    flush(fixture);
    openPanel(fixture);
    type(fixture, 'filter-operation', 'Rent');
    type(fixture, 'filter-location', ' Roma ');
    type(fixture, 'landing-minPrice', '10000');
    type(fixture, 'landing-currency', 'MXN');
    click(fixture, '#landing-amenity-Elevator');

    search(fixture);

    expect(navigate).toHaveBeenLastCalledWith([], expect.objectContaining({
      replaceUrl: true,
      queryParams: { operacion: 'Rent', ubicacion: 'Roma', precioMin: '10000', moneda: 'MXN', amenidades: 'Elevator' },
    }));
  });

  it('writes the page in the address when the visitor changes page, and leaves it out on page 1', () => {
    const fixture = open();
    flush(fixture, [sample()], 30);

    const next = fixture.nativeElement.querySelector('app-paginator [aria-label="Página siguiente"]') as HTMLButtonElement;
    next.click();
    fixture.detectChanges();

    expect(navigate).toHaveBeenLastCalledWith([], expect.objectContaining({ queryParams: { pagina: '2' } }));
    expect(http.expectOne((r) => r.url === URL).request.params.get('page')).toBe('2');
  });

  it('applies the filters that come in the address when the page opens, and opens the panel', () => {
    const fixture = open({
      operacion: 'Rent',
      tipo: 'Apartment',
      ubicacion: 'Roma',
      precioMax: '20000',
      moneda: 'MXN',
      recamaras: '2',
      mascotas: '1',
      amenidades: 'Pool,Gym',
      orden: 'PriceAsc',
      pagina: '2',
    });

    const request = flush(fixture, [sample()], 30);

    expect(request.params.get('operation')).toBe('Rent');
    expect(request.params.get('type')).toBe('Apartment');
    expect(request.params.get('location')).toBe('Roma');
    expect(request.params.get('maxPrice')).toBe('20000');
    expect(request.params.get('currency')).toBe('MXN');
    expect(request.params.get('minBedrooms')).toBe('2');
    expect(request.params.get('petsAllowed')).toBe('true');
    expect(request.params.getAll('amenities')).toEqual(['Pool', 'Gym']);
    expect(request.params.get('sort')).toBe('PriceAsc');
    expect(request.params.get('page')).toBe('2');

    // El formulario refleja lo que trae el enlace, y el panel se abre solo.
    expect(el<HTMLSelectElement>(fixture, '#filter-operation').value).toBe('Rent');
    expect(el<HTMLInputElement>(fixture, '#filter-location').value).toBe('Roma');
    expect(el(fixture, '#advanced-panel')).not.toBeNull();
    expect(el<HTMLInputElement>(fixture, '#landing-amenity-Pool').checked).toBe(true);
    expect(el<HTMLInputElement>(fixture, '#landing-amenity-Elevator').checked).toBe(false);
    expect(el(fixture, '[data-testid="advanced-count"]').textContent).toContain('5');
    // Al abrir un enlace no se reescribe la dirección.
    expect(navigate).not.toHaveBeenCalled();
  });

  it('ignores whatever in the address it does not recognize', () => {
    const fixture = open({
      operacion: 'Lease',
      tipo: 'Castle',
      precioMin: '-5',
      recamaras: 'abc',
      moneda: 'EUR',
      amenidades: 'Jacuzzi,Pool',
      orden: 'cheapest',
      pagina: '-3',
      mascotas: 'si',
    });

    const request = flush(fixture);

    expect(request.params.has('operation')).toBe(false);
    expect(request.params.has('type')).toBe(false);
    expect(request.params.has('minPrice')).toBe(false);
    expect(request.params.has('currency')).toBe(false);
    expect(request.params.has('minBedrooms')).toBe(false);
    expect(request.params.has('petsAllowed')).toBe(false);
    expect(request.params.has('sort')).toBe(false);
    expect(request.params.getAll('amenities')).toEqual(['Pool']);
    expect(request.params.get('page')).toBe('1');
  });

  it('reacts to the address changing from outside (for example the logo), and not to its own writes', () => {
    const fixture = open({ operacion: 'Rent', recamaras: '2' });
    flush(fixture);

    // Alguien pulsa el logo: la dirección queda sin parámetros.
    params$.next(convertToParamMap({}));
    fixture.detectChanges();

    const request = http.expectOne((r) => r.url === URL).request;
    expect(request.params.has('operation')).toBe(false);
    expect(request.params.has('minBedrooms')).toBe(false);
    expect(el<HTMLSelectElement>(fixture, '#filter-operation').value).toBe('');

    // La propia pantalla escribe la dirección después de buscar: eso no vuelve a cargar.
    type(fixture, 'filter-operation', 'Sale');
    search(fixture);
    http.expectOne((r) => r.url === URL);
    params$.next(convertToParamMap({ operacion: 'Sale' }));
    http.expectNone((r) => r.url === URL);
  });
});
