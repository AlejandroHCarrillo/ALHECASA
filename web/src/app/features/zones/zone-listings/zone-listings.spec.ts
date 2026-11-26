import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../../environments/environment';
import { PagedResult } from '../../../shared/models/paged-result';
import { PublicProperty } from '../../landing/public-properties.service';
import { Zone } from '../zone.models';
import { ZoneListings } from './zone-listings';

const zone = (overrides: Partial<Zone> = {}): Zone => ({
  cityName: 'Guadalajara',
  citySlug: 'guadalajara',
  stateName: 'Jalisco',
  neighborhoodName: null,
  neighborhoodSlug: null,
  saleCount: 3,
  rentCount: 1,
  neighborhoods: [
    { name: 'Providencia', cityName: 'Guadalajara', citySlug: 'guadalajara', neighborhoodSlug: 'providencia', saleCount: 2, rentCount: 0 },
    { name: 'Americana', cityName: 'Guadalajara', citySlug: 'guadalajara', neighborhoodSlug: 'americana', saleCount: 0, rentCount: 1 },
  ],
  ...overrides,
});

const property = (id: string, overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id,
  title: `Casa ${id}`,
  description: null,
  type: 'House',
  operation: 'Sale',
  price: 1_000_000,
  currency: 'MXN',
  neighborhood: 'Providencia',
  city: 'Guadalajara',
  state: 'Jalisco',
  bedrooms: 3,
  bathrooms: 2,
  constructionArea: 100,
  landArea: null,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: ['img-1'],
  agencyName: 'Demo',
  agencyId: 'a-1',
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  isFeatured: false,
  ...overrides,
});

const page = (items: PublicProperty[], totalCount = items.length): PagedResult<PublicProperty> => ({
  items,
  page: 1,
  pageSize: 12,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 12)),
});

describe('ZoneListings', () => {
  let http: HttpTestingController;

  const open = async (url: string) => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'venta/:city', component: ZoneListings, data: { operation: 'venta' } },
          { path: 'venta/:city/:neighborhood', component: ZoneListings, data: { operation: 'venta' } },
          { path: 'renta/:city', component: ZoneListings, data: { operation: 'renta' } },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return RouterTestingHarness.create().then(async (harness) => {
      await harness.navigateByUrl(url, ZoneListings);
      return harness;
    });
  };

  const zoneUrl = (...parts: string[]) => `${environment.apiUrl}/public/zones/${parts.join('/')}`;
  const listing = (): TestRequest => http.expectOne((r) => r.url === `${environment.apiUrl}/public/properties`);
  const root = (h: RouterTestingHarness) => h.routeNativeElement as HTMLElement;
  const testId = (h: RouterTestingHarness, id: string) => root(h).querySelector(`[data-testid="${id}"]`);
  const meta = (selector: string) => document.head.querySelector(selector)?.getAttribute('content') ?? null;
  const canonical = () => document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('loads the zone and lists its sale properties, asking by slug, with no token', async () => {
    const harness = await open('/venta/guadalajara');

    const zoneRequest = http.expectOne(zoneUrl('guadalajara'));
    expect(zoneRequest.request.headers.has('Authorization')).toBe(false);
    zoneRequest.flush(zone());

    const request = listing();
    expect(request.request.params.get('citySlug')).toBe('guadalajara');
    expect(request.request.params.get('operation')).toBe('Sale');
    expect(request.request.params.has('neighborhoodSlug')).toBe(false);
    request.flush(page([property('p-1'), property('p-2')], 3));
    harness.detectChanges();

    expect(testId(harness, 'title')?.textContent).toContain('Propiedades en venta en Guadalajara');
    expect(testId(harness, 'count')?.textContent?.replace(/\s+/g, ' ')).toContain('3 propiedades en venta en Guadalajara, Jalisco.');
    expect(root(harness).querySelectorAll('[data-testid="list"] > li').length).toBe(2);
  });

  it('asks for the neighborhood of the city and names it in the title and the breadcrumb', async () => {
    const harness = await open('/venta/guadalajara/providencia');

    http.expectOne(zoneUrl('guadalajara', 'providencia')).flush(
      zone({ neighborhoodName: 'Providencia', neighborhoodSlug: 'providencia', saleCount: 2, rentCount: 0 }),
    );
    const request = listing();
    expect(request.request.params.get('neighborhoodSlug')).toBe('providencia');
    request.flush(page([property('p-1')]));
    harness.detectChanges();

    expect(testId(harness, 'title')?.textContent).toContain('Propiedades en venta en Providencia, Guadalajara');
    expect(testId(harness, 'crumb-city')?.getAttribute('href')).toBe('/venta/guadalajara');
  });

  it('sets the title, description, canonical address and breadcrumb data for search engines', async () => {
    const harness = await open('/venta/guadalajara/providencia');
    http.expectOne(zoneUrl('guadalajara', 'providencia')).flush(
      zone({ neighborhoodName: 'Providencia', neighborhoodSlug: 'providencia', saleCount: 2, rentCount: 0 }),
    );
    listing().flush(page([property('p-1', { imageIds: ['img-9'] })]));
    harness.detectChanges();

    expect(document.title).toBe('Propiedades en venta en Providencia, Guadalajara · ALHECASA');
    expect(meta('meta[name="description"]')).toContain('2 propiedades en venta en Providencia, Guadalajara, Jalisco');
    expect(canonical()).toBe(`${location.origin}/venta/guadalajara/providencia`);
    expect(meta('meta[name="robots"]')).toBeNull();
    expect(meta('meta[property="og:image"]')).toBe(`${environment.apiUrl}/images/img-9`);

    const jsonLd = JSON.parse(document.head.querySelector('script#seo-json-ld')?.textContent ?? '{}');
    expect(jsonLd['@type']).toBe('BreadcrumbList');
    expect(jsonLd.itemListElement.map((i: { name: string }) => i.name)).toEqual([
      'Inicio',
      'Propiedades en venta en Guadalajara',
      'Providencia',
    ]);
    expect(jsonLd.itemListElement[1].item).toBe(`${location.origin}/venta/guadalajara`);
  });

  it('shows the rent operation on /renta and links to the same zone in the other operation', async () => {
    const harness = await open('/renta/guadalajara');
    http.expectOne(zoneUrl('guadalajara')).flush(zone());
    const request = listing();
    expect(request.request.params.get('operation')).toBe('Rent');
    request.flush(page([property('p-1', { operation: 'Rent' })]));
    harness.detectChanges();

    expect(testId(harness, 'title')?.textContent).toContain('en renta');
    expect(testId(harness, 'tab-venta')?.getAttribute('href')).toBe('/venta/guadalajara');
    expect(testId(harness, 'tab-renta')?.getAttribute('href')).toBe('/renta/guadalajara');
    expect(testId(harness, 'tab-venta')?.textContent).toContain('(3)');
    expect(canonical()).toBe(`${location.origin}/renta/guadalajara`);
  });

  it('asks search engines not to index a zone with nothing in this operation, and offers the other one', async () => {
    const harness = await open('/renta/guadalajara');
    http.expectOne(zoneUrl('guadalajara')).flush(zone({ saleCount: 3, rentCount: 0 }));
    listing().flush(page([]));
    harness.detectChanges();

    expect(testId(harness, 'empty')).not.toBeNull();
    expect(testId(harness, 'see-other')?.getAttribute('href')).toBe('/venta/guadalajara');
    expect(meta('meta[name="robots"]')).toBe('noindex,follow');
  });

  it('links to the other neighborhoods of the city, in the operation that has something', async () => {
    const harness = await open('/venta/guadalajara/providencia');
    http.expectOne(zoneUrl('guadalajara', 'providencia')).flush(
      zone({ neighborhoodName: 'Providencia', neighborhoodSlug: 'providencia' }),
    );
    listing().flush(page([property('p-1')]));
    harness.detectChanges();

    const links = Array.from(root(harness).querySelectorAll('[data-testid="neighbors"] a')) as HTMLAnchorElement[];
    expect(links.map((a) => a.getAttribute('href'))).toEqual(['/renta/guadalajara/americana']); // la colonia actual no se enlaza
    expect(links[0].textContent?.trim()).toBe('Americana (1)');
  });

  it('shows a notice, and asks not to index, when the zone does not exist', async () => {
    const harness = await open('/venta/atlantida');

    http.expectOne(zoneUrl('atlantida')).flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    http.expectNone((r) => r.url === `${environment.apiUrl}/public/properties`);
    expect(testId(harness, 'not-found')).not.toBeNull();
    expect(meta('meta[name="robots"]')).toBe('noindex,follow');
    expect(document.title).toContain('Zona no encontrada');
    expect(canonical()).toBeNull();
  });

  it('shows the error, not the not-found notice, when the zone fails to load', async () => {
    const harness = await open('/venta/guadalajara');

    http.expectOne(zoneUrl('guadalajara')).flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(testId(harness, 'not-found')).toBeNull();
    expect(root(harness).textContent).toContain('El servidor no pudo completar la operación');
  });

  it('asks for the requested page when paginating', async () => {
    const harness = await open('/venta/guadalajara');
    http.expectOne(zoneUrl('guadalajara')).flush(zone({ saleCount: 30 }));
    listing().flush(page([property('p-1')], 30));
    harness.detectChanges();

    (root(harness).querySelector('[aria-label="Página siguiente"]') as HTMLElement).click();

    expect(listing().request.params.get('page')).toBe('2');
  });
});
