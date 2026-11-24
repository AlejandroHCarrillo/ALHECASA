import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { UserRole } from '../models/user-role';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', children: [] },
          { path: 'usuarios', children: [] },
        ]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    auth.login({ email: 'a@b.com', password: 'x' }).subscribe();
    controller.expectOne(`${environment.apiUrl}/auth/login`).flush({
      accessToken: 'jwt-token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: '1', fullName: 'A', email: 'a@b.com', role: UserRole.PlatformAdmin, agencyId: null, agencyName: null },
    });
  });

  it('adds the bearer token to API requests', () => {
    http.get(`${environment.apiUrl}/users`).subscribe();
    const req = controller.expectOne(`${environment.apiUrl}/users`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush([]);
  });

  it('does not send the token to third-party hosts', () => {
    http.get('https://example.com/data').subscribe();
    const req = controller.expectOne('https://example.com/data');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('ends the session and asks to log in again, remembering the screen, when the API answers 401', async () => {
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/usuarios');

    http.get(`${environment.apiUrl}/users`).subscribe({ error: () => undefined });
    controller
      .expectOne(`${environment.apiUrl}/users`)
      .flush({}, { status: 401, statusText: 'Unauthorized' });
    await new Promise((resolve) => setTimeout(resolve));

    expect(auth.isAuthenticated()).toBe(false);
    expect(router.url).toBe('/login?returnUrl=%2Fusuarios');
  });
});
