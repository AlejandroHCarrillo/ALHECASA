import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ZonesService } from './zones.service';

describe('ZonesService', () => {
  let service: ZonesService;
  let http: HttpTestingController;
  const url = `${environment.apiUrl}/public/zones`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ZonesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for a city, with no token', () => {
    service.get('guadalajara').subscribe();

    const request = http.expectOne(`${url}/guadalajara`);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('asks for a neighborhood of a city, encoding the parts', () => {
    service.get('guadalajara', 'la americana').subscribe();

    http.expectOne(`${url}/guadalajara/la%20americana`).flush({});
  });

  it('asks for the top zones with a limit', () => {
    service.top(12).subscribe();

    const request = http.expectOne((r) => r.url === url);
    expect(request.request.params.get('limit')).toBe('12');
    request.flush([]);
  });
});
