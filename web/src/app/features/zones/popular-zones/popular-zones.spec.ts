import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { ZoneLink } from '../zone.models';
import { PopularZones } from './popular-zones';

const link = (overrides: Partial<ZoneLink>): ZoneLink => ({
  name: 'Guadalajara',
  cityName: 'Guadalajara',
  citySlug: 'guadalajara',
  neighborhoodSlug: null,
  saleCount: 4,
  rentCount: 1,
  ...overrides,
});

describe('PopularZones', () => {
  let http: HttpTestingController;

  const create = () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PopularZones);
    fixture.detectChanges();
    return fixture;
  };

  const request = () => http.expectOne((r) => r.url === `${environment.apiUrl}/public/zones`);

  it('links every zone to its page, in the operation with more properties', () => {
    const fixture = create();
    request().flush([
      link({}),
      link({ name: 'Americana', neighborhoodSlug: 'americana', saleCount: 0, rentCount: 5 }),
    ]);
    fixture.detectChanges();

    const links = Array.from(fixture.nativeElement.querySelectorAll('a') as NodeListOf<HTMLAnchorElement>);
    expect(links.map((a) => a.getAttribute('href'))).toEqual(['/venta/guadalajara', '/renta/guadalajara/americana']);
    expect(links[0].textContent?.trim()).toBe('Guadalajara (5)');
    expect(links[1].textContent?.trim()).toBe('Americana, Guadalajara (5)');
  });

  it('shows nothing when there are no zones', () => {
    const fixture = create();
    request().flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="popular-zones"]')).toBeNull();
  });

  it('shows nothing, and no error, when the zones fail to load', () => {
    const fixture = create();
    request().flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });
});
