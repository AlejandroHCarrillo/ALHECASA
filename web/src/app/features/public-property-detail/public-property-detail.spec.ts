import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../environments/environment';
import { PublicProperty } from '../landing/public-properties.service';
import { LEAFLET_LOADER } from '../../shared/components/location-map/location-map';
import { PublicPropertyDetail } from './public-property-detail';

const sample = (overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id: 'p-1',
  title: 'Casa moderna con alberca',
  description: 'Tres niveles y jardín.\nCerca de todo.',
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
  landArea: null,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: ['img-a', 'img-b', 'img-c'],
  agencyName: 'Demo Real Estate',
  agencyId: 'agency-1',
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  isFeatured: false,
  ...overrides,
});

describe('PublicPropertyDetail', () => {
  let http: HttpTestingController;

  const open = async (url = '/publicaciones/p-1') => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'publicaciones/:id', component: PublicPropertyDetail }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        // El mapa real no se carga en estas pruebas: aquí solo importa si el detalle lo muestra o no.
        { provide: LEAFLET_LOADER, useValue: () => new Promise(() => undefined) },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, PublicPropertyDetail);
    return harness;
  };

  const detailUrl = (id: string) => `${environment.apiUrl}/public/properties/${id}`;
  const root = (harness: RouterTestingHarness) => harness.routeNativeElement as HTMLElement;
  const mainImage = (harness: RouterTestingHarness) =>
    (root(harness).querySelector('[data-testid="main-image"]') as HTMLImageElement).getAttribute('src');
  const click = (harness: RouterTestingHarness, selector: string) => {
    (root(harness).querySelector(selector) as HTMLElement).click();
    harness.detectChanges();
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('asks the public endpoint, with no token, and shows the full detail', async () => {
    const harness = await open();

    const request = http.expectOne(detailUrl('p-1'));
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(sample());
    harness.detectChanges();

    const text = root(harness).textContent as string;
    expect(text).toContain('Casa moderna con alberca');
    expect(text).toContain('En venta');
    expect(text).toContain('$18,500,000');
    expect(text).toContain('Lomas, Ciudad de México, CDMX');
    expect(text).toContain('Tres niveles y jardín.');
    expect(text).toContain('520 m²');
    expect(text.replace(/\s+/g, ' ')).toContain('Publica: Demo Real Estate');
    expect(text).not.toContain('Terreno'); // sin dato, no se muestra
  });

  it('links the publisher to all of its active publications, but not an individual', async () => {
    const company = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ agencyName: 'Demo Real Estate', agencyId: 'agency-7' }));
    company.detectChanges();
    const link = root(company).querySelector('[data-testid="agency-link"]') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Demo Real Estate');
    expect(link.getAttribute('href')).toBe('/inmobiliarias/agency-7');

    TestBed.resetTestingModule();
    const individual = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ agencyName: 'Particular', agencyId: null }));
    individual.detectChanges();
    expect(root(individual).querySelector('[data-testid="agency-link"]')).toBeNull();
    expect((root(individual).textContent as string).replace(/\s+/g, ' ')).toContain('Publica: Particular');
  });
  it('badges a featured publication and only a featured one', async () => {
    const featured = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ isFeatured: true }));
    featured.detectChanges();
    expect(root(featured).querySelector('[data-testid="featured-badge"]')?.textContent).toContain('Destacado');

    TestBed.resetTestingModule();
    const normal = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ isFeatured: false }));
    normal.detectChanges();
    expect(root(normal).querySelector('[data-testid="featured-badge"]')).toBeNull();
  });

  it('shows every photo as a thumbnail and the first one large', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();

    expect(mainImage(harness)).toBe(`${environment.apiUrl}/images/img-a`);
    expect(root(harness).querySelectorAll('ul[aria-label="Todas las fotos"] li')).toHaveLength(3);
    expect(root(harness).querySelector('[data-testid="counter"]')?.textContent).toContain('1 / 3');
  });

  it('changes the large photo from a thumbnail and with next/previous, wrapping around', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();

    click(harness, 'button[aria-label="Ver foto 3"]');
    expect(mainImage(harness)).toBe(`${environment.apiUrl}/images/img-c`);

    click(harness, 'button[aria-label="Foto siguiente"]');
    expect(mainImage(harness)).toBe(`${environment.apiUrl}/images/img-a`);

    click(harness, 'button[aria-label="Foto anterior"]');
    expect(mainImage(harness)).toBe(`${environment.apiUrl}/images/img-c`);
    expect(root(harness).querySelector('[data-testid="counter"]')?.textContent).toContain('3 / 3');
  });

  it('with a single photo there are no arrows nor thumbnails', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ imageIds: ['solo'] }));
    harness.detectChanges();

    expect(mainImage(harness)).toBe(`${environment.apiUrl}/images/solo`);
    expect(root(harness).querySelector('button[aria-label="Foto siguiente"]')).toBeNull();
    expect(root(harness).querySelector('ul[aria-label="Todas las fotos"]')).toBeNull();
  });

  it('shows a placeholder when there are no photos', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ imageIds: [] }));
    harness.detectChanges();

    expect(root(harness).querySelector('[data-testid="no-photos"]')).not.toBeNull();
    expect(root(harness).querySelector('[data-testid="main-image"]')).toBeNull();
  });

  it('tells the visitor when the publication is no longer available (404)', async () => {
    const harness = await open('/publicaciones/gone');
    http.expectOne(detailUrl('gone')).flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    expect(root(harness).querySelector('[data-testid="not-found"]')?.textContent).toContain('ya no está disponible');
  });

  it('shows the server error for other failures', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    harness.detectChanges();

    expect(root(harness).querySelector('[data-testid="not-found"]')).toBeNull();
    expect(root(harness).querySelector('app-error-alert')).not.toBeNull();
  });

  it('lets the visitor like the publication from the detail, sharing the state with the list', async () => {
    localStorage.setItem('alhecasa.likes', JSON.stringify(['otra']));
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();
    const heart = root(harness).querySelector('app-like-button button') as HTMLButtonElement;
    expect(heart.getAttribute('aria-pressed')).toBe('false');
    expect(heart.textContent).toContain('Me gusta');

    heart.click();
    harness.detectChanges();

    expect(heart.getAttribute('aria-pressed')).toBe('true');
    expect(heart.textContent).toContain('Te gusta');
    expect(JSON.parse(localStorage.getItem('alhecasa.likes') ?? '[]')).toEqual(['otra', 'p-1']);
  });

  it('shows the publication as liked when it was liked before', async () => {
    localStorage.setItem('alhecasa.likes', JSON.stringify(['p-1']));
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();

    expect(root(harness).querySelector('app-like-button button')?.getAttribute('aria-pressed')).toBe('true');
  });

  it('shows the map with the approximate zone when the property has a location', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ approximateLatitude: 19.433, approximateLongitude: -99.133 }));
    harness.detectChanges();

    const section = root(harness).querySelector('[data-testid="location"]');
    expect(section).not.toBeNull();
    expect(section?.querySelector('app-location-map')).not.toBeNull();
    expect(section?.textContent).toContain('zona aproximada');
    expect(section?.querySelector('[role="region"]')?.getAttribute('aria-label')).toContain('Casa moderna con alberca');
  });

  it('does not show a map when the property has no location', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();

    expect(root(harness).querySelector('[data-testid="location"]')).toBeNull();
    expect(root(harness).querySelector('app-location-map')).toBeNull();
  });

  it('offers the login only to visitors without a session', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();

    expect(root(harness).querySelectorAll('a[href="/login"]')).toHaveLength(1);
  });
  it('offers "Pedir informes" and opens the request form for that property', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();
    expect(root(harness).querySelector('app-inquiry-form-modal')).toBeNull();

    click(harness, '[data-testid="request-info"]');

    expect(root(harness).querySelector('app-inquiry-form-modal')).not.toBeNull();
    expect(root(harness).querySelector('dialog')?.textContent).toContain('Casa moderna con alberca');
  });

  it('shows parking, pets, furnished and the amenities with their Spanish names', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ parkingSpaces: 3, petsAllowed: true, furnished: true, amenities: ['Pool', 'Security24h'] }));
    harness.detectChanges();

    expect(root(harness).querySelector('[data-testid="parking"]')?.textContent).toContain('3');
    const features = root(harness).querySelector('[data-testid="features"]')?.textContent ?? '';
    expect(features).toContain('Amueblada');
    expect(features).toContain('Acepta mascotas');
    expect(features).toContain('Alberca');
    expect(features).toContain('Seguridad 24 h');
    expect(features).not.toContain('Gimnasio');
  });

  it('does not show the features block, nor parking, when there is nothing to show', async () => {
    const harness = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    harness.detectChanges();

    expect(root(harness).querySelector('[data-testid="features"]')).toBeNull();
    expect(root(harness).querySelector('[data-testid="parking"]')).toBeNull();
  });

  it('badges the publication as featured only when it is', async () => {
    const featured = await open();
    http.expectOne(detailUrl('p-1')).flush(sample({ isFeatured: true }));
    featured.detectChanges();
    expect(root(featured).querySelector('[data-testid="featured-badge"]')?.textContent).toContain('Destacado');

    TestBed.resetTestingModule();
    const plain = await open();
    http.expectOne(detailUrl('p-1')).flush(sample());
    plain.detectChanges();
    expect(root(plain).querySelector('[data-testid="featured-badge"]')).toBeNull();
  });
});
