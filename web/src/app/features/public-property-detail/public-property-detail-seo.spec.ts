import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../environments/environment';
import { LEAFLET_LOADER } from '../../shared/components/location-map/location-map';
import { PublicProperty } from '../landing/public-properties.service';
import { PublicPropertyDetail } from './public-property-detail';

const sample = (overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id: 'p-1',
  title: 'Casa moderna con alberca',
  description: 'Tres niveles y jardín.',
  type: 'House',
  operation: 'Sale',
  price: 18_500_000,
  currency: 'MXN',
  neighborhood: 'Providencia',
  city: 'Guadalajara',
  state: 'Jalisco',
  bedrooms: 4,
  bathrooms: 5,
  constructionArea: 520,
  landArea: null,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: ['img-a'],
  agencyName: 'Demo',
  agencyId: 'agency-1',
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  isFeatured: false,
  citySlug: 'guadalajara',
  neighborhoodSlug: 'providencia',
  ...overrides,
});

describe('PublicPropertyDetail: SEO and zone links', () => {
  let http: HttpTestingController;

  const open = async (property: PublicProperty) => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'publicaciones/:id', component: PublicPropertyDetail }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: LEAFLET_LOADER, useValue: () => new Promise(() => undefined) },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/publicaciones/p-1', PublicPropertyDetail);
    http.expectOne(`${environment.apiUrl}/public/properties/p-1`).flush(property);
    harness.detectChanges();
    return harness;
  };

  const meta = (selector: string) => document.head.querySelector(selector)?.getAttribute('content') ?? null;
  const links = (harness: RouterTestingHarness) =>
    Array.from((harness.routeNativeElement as HTMLElement).querySelectorAll('[data-testid="zone-links"] a')) as HTMLAnchorElement[];

  beforeEach(() => localStorage.clear());

  it('sets the title, description, canonical address and image of the listing', async () => {
    await open(sample());

    expect(document.title).toBe('Casa moderna con alberca · ALHECASA');
    expect(meta('meta[name="description"]')).toBe('Tres niveles y jardín.');
    expect(document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe(`${location.origin}/publicaciones/p-1`);
    expect(meta('meta[property="og:image"]')).toBe(`${environment.apiUrl}/images/img-a`);
  });

  it('shortens a long description and describes a listing that has none', async () => {
    await open(sample({ description: 'x'.repeat(400) }));
    expect(meta('meta[name="description"]')?.length).toBeLessThanOrEqual(155);
    expect(meta('meta[name="description"]')).toMatch(/\.\.\.$/);

    TestBed.resetTestingModule();
    await open(sample({ description: null }));
    expect(meta('meta[name="description"]')).toBe('Casa en venta en Providencia, Guadalajara');
  });

  it('links to the pages of its neighborhood and its city, in its own operation', async () => {
    const sale = await open(sample());
    expect(links(sale).map((a) => [a.textContent?.trim(), a.getAttribute('href')])).toEqual([
      ['Propiedades en venta en Providencia', '/venta/guadalajara/providencia'],
      ['Propiedades en venta en Guadalajara', '/venta/guadalajara'],
    ]);

    TestBed.resetTestingModule();
    const rent = await open(sample({ operation: 'Rent' }));
    expect(links(rent).map((a) => a.getAttribute('href'))).toEqual(['/renta/guadalajara/providencia', '/renta/guadalajara']);
  });

  it('links only to the city when it has no neighborhood, and to nothing when it is not in the catalog', async () => {
    const city = await open(sample({ neighborhood: null, neighborhoodSlug: null }));
    expect(links(city).map((a) => a.getAttribute('href'))).toEqual(['/venta/guadalajara']);

    TestBed.resetTestingModule();
    const none = await open(sample({ citySlug: null, neighborhoodSlug: null }));
    expect(links(none)).toEqual([]);
  });
});
