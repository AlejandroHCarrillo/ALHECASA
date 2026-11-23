import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { UserRole } from '../models/user-role';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;

  const setup = () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'login', children: [] }]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  };

  beforeEach(() => localStorage.clear());

  it('stores the session after a successful login', () => {
    const auth = setup();

    auth.login({ email: 'a@b.com', password: 'x' }).subscribe();
    http.expectOne(`${environment.apiUrl}/auth/login`).flush({
      accessToken: 'jwt',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: '1', fullName: 'A', email: 'a@b.com', role: UserRole.AgencyAdmin, agencyId: 'x', agencyName: 'X' },
    });

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.token()).toBe('jwt');
    expect(auth.hasRole(UserRole.AgencyAdmin)).toBe(true);
    expect(auth.hasRole(UserRole.PlatformAdmin)).toBe(false);
  });

  it('ignores and clears an expired stored session', () => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({ token: 't', expiresAt: new Date(Date.now() - 1000).toISOString(), user: { id: '1' } }),
    );

    const auth = setup();

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('alhecasa.session')).toBeNull();
  });

  it('clears the session on logout', () => {
    const auth = setup();
    auth.login({ email: 'a@b.com', password: 'x' }).subscribe();
    http.expectOne(`${environment.apiUrl}/auth/login`).flush({
      accessToken: 'jwt',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: '1', fullName: 'A', email: 'a@b.com', role: UserRole.Client, agencyId: null, agencyName: null },
    });

    auth.logout();

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('alhecasa.session')).toBeNull();
  });
});
