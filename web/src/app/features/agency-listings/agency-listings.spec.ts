import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../environments/environment';
import { PublicProperty } from '../landing/public-properties.service';
import { AgencyListings } from './agency-listings';

const URL = `${environment.apiUrl}/public/properties`;

const property = (id: string, overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id,
  title: `Casa ${id}`,
  description: null,
  type: 'House',
  operation: 'Sale',
  price: 1_000_000,
  currency: 'MXN',
  neighborhood: null,
  city: 'Puebla',
  state: 'Puebla',
  bedrooms: null,
  bathrooms: null,
  constructionArea: null,
  landArea: null,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: [],
  agencyName: 'Demo Real Estate',
  agencyId: 'agency-7',
  parkingSpaces: null,
  petsAllowed: false,
  furnished: false,
  amenities: [],
  isFeatured: false,
  ...overrides,
});

const page = (items: PublicProperty[], total = items.length, pageNumber = 1) => ({
  items,
  page: pageNumber,
  pageSize: 12,
  totalCount: total,
  totalPages: Math.max(1, Math.ceil(total / 12)),
});

describe('AgencyListings', () => {
  let http: HttpTestingController;

  const open = async (id = 'agency-7') => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'inmobiliarias/:id', component: AgencyListings }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/inmobiliarias/${id}`, AgencyListings);
    return harness;
  };

  const list = () => http.expectOne((r) => r.url === URL);
  const root = (harness: RouterTestingHarness) => harness.routeNativeElement as HTMLElement;
  const byId = (harness: RouterTestingHarness, id: string) => root(harness).querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('asks the public endpoint, with no token, for that agency only', async () => {
    const harness = await open('agency-7');

    const request = list();
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.params.get('agencyId')).toBe('agency-7');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('12');
    request.flush(page([]));
    harness.detectChanges();
  });

  it('shows the agency name, how many active publications it has, and their cards', async () => {
    const harness = await open();
    list().flush(page([property('p-1'), property('p-2')], 2));
    harness.detectChanges();

    expect(byId(harness, 'title')?.textContent).toContain('Publicaciones de Demo Real Estate');
    expect(byId(harness, 'count')?.textContent).toContain('2 publicaciones activas');
    expect(root(harness).querySelectorAll('app-public-property-card')).toHaveLength(2);
    expect(root(harness).textContent).toContain('Casa p-1');
  });

  it('uses the singular for a single publication', async () => {
    const harness = await open();
    list().flush(page([property('p-1')], 1));
    harness.detectChanges();

    expect(byId(harness, 'count')?.textContent).toContain('1 publicación activa');
  });

  it('says so, without an error, when the agency has no active publications or the link matches none', async () => {
    const harness = await open('desconocida');
    list().flush(page([]));
    harness.detectChanges();

    expect(byId(harness, 'empty')?.textContent).toContain('no tiene publicaciones activas');
    expect(byId(harness, 'title')?.textContent).toContain('Publicaciones de la inmobiliaria');
    expect(root(harness).querySelector('app-error-alert')).toBeNull();
  });

  it('shows the server error when the request fails, not the empty notice', async () => {
    const harness = await open('no-es-un-guid');
    list().flush({ title: 'Bad Request', status: 400 }, { status: 400, statusText: 'Bad Request' });
    harness.detectChanges();

    expect(root(harness).querySelector('app-error-alert')).not.toBeNull();
    expect(byId(harness, 'empty')).toBeNull();
  });

  it('asks for the next page keeping the agency', async () => {
    const harness = await open();
    list().flush(page([property('p-1')], 30));
    harness.detectChanges();

    (root(harness).querySelector('button[aria-label="Página siguiente"]') as HTMLButtonElement).click();
    const request = list();
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('agencyId')).toBe('agency-7');
    request.flush(page([property('p-13')], 30, 2));
    harness.detectChanges();

    expect(root(harness).textContent).toContain('Casa p-13');
  });

  it('offers the login only to visitors without a session', async () => {
    const visitor = await open();
    list().flush(page([]));
    visitor.detectChanges();
    expect(root(visitor).querySelector('a[href="/login"]')).not.toBeNull();

    TestBed.resetTestingModule();
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Me', email: 'me@a.com', role: 'Seller', agencyId: 'agency-1', agencyName: null },
      }),
    );
    const member = await open();
    list().flush(page([]));
    member.detectChanges();
    expect(root(member).querySelector('a[href="/login"]')).toBeNull();
  });

  describe('información de la inmobiliaria', () => {
    const AGENCY_URL = `${environment.apiUrl}/public/agencies/agency-7`;
    const info = {
      id: 'agency-7', name: 'Norte Configurada', email: 'contacto@norte.test', phone: '55 1234 5678', description: 'Casas y departamentos.',
      website: null, facebookUrl: null, instagramUrl: null, openingHours: 'Lun a Vie 9 a 18', street: null, city: 'Puebla', state: 'Puebla',
      postalCode: null, hasLogo: false,
    };

    it('asks the public endpoint for it, with no token', async () => {
      const harness = await open('agency-7');

      const request = http.expectOne(AGENCY_URL);
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush(info);
      list().flush(page([]));
      harness.detectChanges();
    });

    it('shows what the agency configured above its publications, and uses that name', async () => {
      const harness = await open('agency-7');
      http.expectOne(AGENCY_URL).flush(info);
      list().flush(page([property('p-1')]));
      harness.detectChanges();

      expect(byId(harness, 'agency-info')).not.toBeNull();
      expect(byId(harness, 'agency-name')?.textContent).toBe('Norte Configurada');
      expect(byId(harness, 'description')?.textContent).toContain('Casas y departamentos.');
      expect(byId(harness, 'hours')?.textContent).toContain('Lun a Vie 9 a 18');
      expect(byId(harness, 'title')?.textContent).toContain('Publicaciones de Norte Configurada');
    });

    it('still lists the publications when the information cannot be loaded', async () => {
      const harness = await open('agency-7');
      http.expectOne(AGENCY_URL).flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
      list().flush(page([property('p-1')]));
      harness.detectChanges();

      expect(byId(harness, 'agency-info')).toBeNull();
      expect(byId(harness, 'title')?.textContent).toContain('Publicaciones de Demo Real Estate');
      expect(root(harness).querySelectorAll('app-public-property-card')).toHaveLength(1);
      expect(root(harness).querySelector('app-error-alert')).toBeNull();
    });
  });});
