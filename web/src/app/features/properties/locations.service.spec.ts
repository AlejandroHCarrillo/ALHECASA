import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { LocationsService } from './locations.service';

describe('LocationsService', () => {
  let service: LocationsService;
  let http: HttpTestingController;
  const url = `${environment.apiUrl}/locations`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(LocationsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the states', () => {
    let result: string[] = [];
    service.states().subscribe((r) => (result = r));

    http.expectOne(`${url}/states`).flush(['Jalisco']);
    expect(result).toEqual(['Jalisco']);
  });

  it('searches cities by state and text, sending only what has text', () => {
    service.cities(' Jalisco ', 'Gua').subscribe();
    const filtered = http.expectOne((r) => r.url === `${url}/cities`);
    expect(filtered.request.params.get('state')).toBe('Jalisco');
    expect(filtered.request.params.get('q')).toBe('Gua');
    filtered.flush([]);

    service.cities('', '  ').subscribe();
    const open = http.expectOne((r) => r.url === `${url}/cities`);
    expect(open.request.params.keys()).toEqual([]);
    open.flush([]);
  });

  it('searches neighborhoods by state, city and text', () => {
    service.neighborhoods('Jalisco', 'Guadalajara', 'Prov').subscribe();

    const request = http.expectOne((r) => r.url === `${url}/neighborhoods`);
    expect(request.request.params.get('state')).toBe('Jalisco');
    expect(request.request.params.get('city')).toBe('Guadalajara');
    expect(request.request.params.get('q')).toBe('Prov');
    request.flush([]);
  });
});
