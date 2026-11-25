import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { PublicProperty } from '../landing/public-properties.service';
import { Gallery } from './gallery';

const URL = `${environment.apiUrl}/properties/gallery`;

const sample = (id: string, overrides: Partial<PublicProperty> = {}): PublicProperty => ({
  id,
  title: `Casa ${id}`,
  description: null,
  type: 'House',
  operation: 'Sale',
  price: 1_000_000,
  currency: 'MXN',
  neighborhood: null,
  city: 'CDMX',
  state: 'CDMX',
  bedrooms: null,
  bathrooms: null,
  constructionArea: null,
  landArea: null,
  approximateLatitude: null,
  approximateLongitude: null,
  imageIds: [],
  agencyName: 'Demo Real Estate',
  ...overrides,
});

const page = (items: PublicProperty[], total = items.length, pageNumber = 1) => ({
  items,
  page: pageNumber,
  pageSize: 12,
  totalCount: total,
  totalPages: Math.max(1, Math.ceil(total / 12)),
});

describe('Gallery', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<Gallery>;

  const create = (): void => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Gallery);
    fixture.detectChanges();
  };

  const text = () => fixture.nativeElement.textContent as string;

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('asks for the first page of the own gallery', () => {
    create();

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('12');
    request.flush(page([]));
  });

  it('shows each published property with the same card as the public site: cover, link to the public detail and heart', () => {
    create();
    http.expectOne((r) => r.url === URL).flush(page([sample('p-1', { imageIds: ['img-1', 'img-2'] }), sample('p-2')]));
    fixture.detectChanges();

    const cards = fixture.nativeElement.querySelectorAll('app-public-property-card');
    expect(cards).toHaveLength(2);
    expect(text()).toContain('Casa p-1');
    expect(text()).toContain('$1,000,000');
    expect(fixture.nativeElement.querySelector('a[href="/publicaciones/p-1"] [data-testid="cover"] img')).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('app-like-button button')).toHaveLength(2);
  });

  it('explains how to fill the gallery when nothing is published', () => {
    create();
    http.expectOne((r) => r.url === URL).flush(page([]));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="empty"]')?.textContent).toContain('Todavía no tienes propiedades publicadas');
    expect(fixture.nativeElement.querySelector('[data-testid="gallery-list"]')).toBeNull();
  });

  it('pages through the gallery', () => {
    create();
    http.expectOne((r) => r.url === URL).flush(page([sample('p-1')], 13));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[aria-label="Página siguiente"]') as HTMLButtonElement).click();

    const request = http.expectOne((r) => r.url === URL);
    expect(request.request.params.get('page')).toBe('2');
    request.flush(page([sample('p-13')], 13, 2));
    fixture.detectChanges();
    expect(text()).toContain('Casa p-13');
  });

  it('shows the server error', () => {
    create();
    http.expectOne((r) => r.url === URL).flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-error-alert')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="empty"]')).toBeNull();
  });
});