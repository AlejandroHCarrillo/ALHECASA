import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { environment } from '../../../environments/environment';
import { guestMatch } from '../../core/auth/auth.guards';
import { Landing } from './landing';
import { PublicProperty } from './public-properties.service';

const URL = `${environment.apiUrl}/public/properties`;

const sample = (overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id: 'p-1',
  title: 'Casa moderna con alberca',
  description: 'Tres niveles y jardín.',
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
  imageIds: [],
  agencyName: 'Demo Real Estate',
  ...overrides,
});

const page = (items: PublicProperty[], total = items.length, pageNumber = 1) => ({
  items,
  page: pageNumber,
  pageSize: 12,
  totalCount: total,
  totalPages: Math.ceil(total / 12),
});

describe('Landing', () => {
  let http: HttpTestingController;

  const open = async (): Promise<ComponentFixture<Landing>> => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(Landing);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('asks the public endpoint, with no token, and shows the published properties', async () => {
    const fixture = await open();

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.params.get('pageSize')).toBe('12');
    request.flush(page([sample(), sample({ id: 'p-2', title: 'Loft en la Roma', operation: 'Rent', price: 22_000 })]));
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Casa moderna con alberca');
    expect(text).toContain('Loft en la Roma');
    expect(text).toContain('$18,500,000');
    expect(text).toContain('En venta');
    expect(text).toContain('En renta');
    expect(text).toContain('/mes');
    expect(text).toContain('Lomas, Ciudad de México, CDMX');
    expect(text).toContain('Publica: Demo Real Estate');
  });

  it('shows the first image as the cover, how many photos there are, and a placeholder without photos', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(
      page([
        sample({ id: 'p-1', title: 'Con fotos', imageIds: ['img-a', 'img-b', 'img-c'] }),
        sample({ id: 'p-2', title: 'Con una foto', imageIds: ['img-d'] }),
        sample({ id: 'p-3', title: 'Sin fotos', imageIds: [] }),
      ]),
    );
    fixture.detectChanges();

    const covers = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="cover"] img') as NodeListOf<HTMLImageElement>);
    expect(covers.map((img) => img.getAttribute('src'))).toEqual([
      `${environment.apiUrl}/images/img-a`,
      `${environment.apiUrl}/images/img-d`,
    ]);
    expect(covers[0].getAttribute('alt')).toBe('Foto de Con fotos');
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('3 fotos');
    expect(text).not.toContain('1 fotos');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="no-cover"]')).toHaveLength(1);
  });

  it('links both the cover and the title of each property to its detail page', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([sample({ id: 'p-9', title: 'Casa enlazada', imageIds: ['img-a'] })]));
    fixture.detectChanges();

    const links = Array.from(fixture.nativeElement.querySelectorAll('a[href="/publicaciones/p-9"]') as NodeListOf<HTMLAnchorElement>);
    expect(links).toHaveLength(2);
    expect(links.some((a) => a.querySelector('[data-testid="cover"]'))).toBe(true);
    expect(links.some((a) => a.textContent?.trim() === 'Casa enlazada')).toBe(true);
  });

  it('links to the login in the header, the introduction and the closing section', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([]));
    fixture.detectChanges();

    const links = Array.from(fixture.nativeElement.querySelectorAll('a[href="/login"]') as NodeListOf<HTMLAnchorElement>);
    expect(links).toHaveLength(3);
    expect(links.every((a) => a.textContent?.includes('Iniciar sesión'))).toBe(true);
  });

  it('shows a message when there are no published properties', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([]));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Todavía no hay propiedades publicadas.');
  });

  const field = (fixture: ComponentFixture<Landing>, id: string, value: string): void => {
    const element = fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement | HTMLSelectElement;
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  };

  const submitFilters = (fixture: ComponentFixture<Landing>): void => {
    (fixture.nativeElement.querySelector('form[role="search"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('offers the filters of the mockup: operation, location and property type', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([]));
    fixture.detectChanges();

    const options = (id: string) =>
      Array.from(fixture.nativeElement.querySelectorAll(`#${id} option`) as NodeListOf<HTMLOptionElement>).map((o) => o.value);

    expect(options('filter-operation')).toEqual(['', 'Sale', 'Rent']);
    expect(options('filter-type')).toEqual(['', 'House', 'Apartment', 'Land', 'Commercial', 'Office']);
    expect(fixture.nativeElement.querySelector('#filter-location')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('form[role="search"] button[type="submit"]').textContent).toContain('Buscar');
  });

  it('does not filter while the visitor is only choosing, and sends every filter on Buscar, from page 1', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([sample()], 30));
    fixture.detectChanges();

    field(fixture, 'filter-operation', 'Rent');
    field(fixture, 'filter-location', '  Polanco ');
    field(fixture, 'filter-type', 'Apartment');
    fixture.detectChanges();
    http.expectNone((r) => r.url === URL);

    submitFilters(fixture);

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.params.get('operation')).toBe('Rent');
    expect(request.request.params.get('type')).toBe('Apartment');
    expect(request.request.params.get('location')).toBe('Polanco');
    expect(request.request.params.get('page')).toBe('1');
    request.flush(page([]));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No hay propiedades que coincidan con los filtros.');
  });

  it('omits the filters left empty', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([]));
    fixture.detectChanges();

    field(fixture, 'filter-type', 'House');
    submitFilters(fixture);

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.params.get('type')).toBe('House');
    expect(request.request.params.has('operation')).toBe(false);
    expect(request.request.params.has('location')).toBe(false);
    request.flush(page([]));
  });

  it('shows the count and lets the visitor remove the filters', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([sample()], 7));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('7 propiedades');
    expect(fixture.nativeElement.textContent).not.toContain('Quitar filtros');

    field(fixture, 'filter-operation', 'Sale');
    submitFilters(fixture);
    http.expectOne((r) => r.url === URL).flush(page([sample()], 1));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('1 propiedad con los filtros elegidos');

    const clear = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find((b) =>
      b.textContent?.includes('Quitar filtros'),
    );
    clear?.click();

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.params.has('operation')).toBe(false);
    request.flush(page([sample()], 7));
    fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('#filter-operation') as HTMLSelectElement).value).toBe('');
    expect(fixture.nativeElement.textContent).not.toContain('Quitar filtros');
  });

  it('goes back to page 1 when the filters change after browsing', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(page([sample()], 30));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[aria-label="Página siguiente"]') as HTMLButtonElement).click();
    const second = http.expectOne((r) => r.url === URL);
    expect(second.request.params.get('page')).toBe('2');
    second.flush(page([sample()], 30, 2));
    fixture.detectChanges();

    field(fixture, 'filter-operation', 'Sale');
    submitFilters(fixture);

    expect(http.expectOne((r) => r.url === URL).request.params.get('page')).toBe('1');
  });

  it('shows the server error without breaking the page', async () => {
    const fixture = await open();
    http.expectOne((r) => r.url === URL).flush(
      { title: 'Server error' },
      { status: 500, statusText: 'Internal Server Error' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Propiedades disponibles');
  });
});

describe('guestMatch: la portada solo coincide sin sesión', () => {
  @Component({ selector: 'app-fake-landing', template: 'PORTADA' })
  class FakeLanding {}

  @Component({ selector: 'app-fake-work-area', template: 'AREA' })
  class FakeWorkArea {}

  const navigate = async (withSession: boolean): Promise<string> => {
    if (withSession) {
      localStorage.setItem(
        'alhecasa.session',
        JSON.stringify({
          token: 'jwt',
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
          user: { id: 'me', fullName: 'Me', email: 'me@a.com', role: 'Seller', agencyId: 'a', agencyName: null },
        }),
      );
    }

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: '', pathMatch: 'full', canMatch: [guestMatch], component: FakeLanding },
          { path: '', pathMatch: 'full', redirectTo: 'inicio' },
          { path: 'inicio', component: FakeWorkArea },
        ]),
      ],
    });

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    return TestBed.inject(Router).url + '|' + (harness.routeNativeElement?.textContent ?? '');
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('shows the landing to a visitor', async () => {
    expect(await navigate(false)).toBe('/|PORTADA');
  });

  it('opens the work area when there is a session', async () => {
    expect(await navigate(true)).toBe('/inicio|AREA');
  });
});
