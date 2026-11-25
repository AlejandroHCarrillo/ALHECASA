import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { UserRole } from '../core/models/user-role';
import { Shell } from './shell';

describe('Shell menu', () => {
  const open = (role: UserRole): string[] => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Me', email: 'me@a.com', role, agencyId: role === UserRole.PlatformAdmin ? null : 'a', agencyName: null },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return Array.from(fixture.nativeElement.querySelectorAll('nav a') as NodeListOf<HTMLAnchorElement>).map((a) => a.getAttribute('aria-label') ?? '');
  };

  const openIndividual = (): string[] => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Ana', email: 'a@a.com', role: UserRole.AgencyAdmin, agencyId: 'a', agencyName: 'Ana', isIndividual: true },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return Array.from(fixture.nativeElement.querySelectorAll('nav a') as NodeListOf<HTMLAnchorElement>).map((a) => a.getAttribute('aria-label') ?? '');
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it.each([UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller])('%s sees the gallery, next to Propiedades', (role) => {
    const items = open(role);

    expect(items).toContain('Galería');
    expect(items.indexOf('Galería')).toBe(items.indexOf('Propiedades') + 1);
  });

  it('the platform administrator has no gallery (it belongs to an agency)', () => {
    expect(open(UserRole.PlatformAdmin)).not.toContain('Galería');
  });

  it('a client sees neither the gallery nor the inventory', () => {
    const items = open(UserRole.Client);

    expect(items).not.toContain('Galería');
    expect(items).not.toContain('Propiedades');
  });
  it.each([UserRole.AgencyAdmin, UserRole.Supervisor, UserRole.Seller])('%s sees the inquiries inbox', (role) => {
    expect(open(role)).toContain('Solicitudes de informes');
  });

  it.each([UserRole.PlatformAdmin, UserRole.Client])('%s does not see the inquiries inbox', (role) => {
    expect(open(role)).not.toContain('Solicitudes de informes');
  });
  it('hides the users option from a free individual account, which has a single user', () => {
    const items = openIndividual();

    expect(items).not.toContain('Usuarios');
    expect(items).toContain('Propiedades');
    expect(items).toContain('Solicitudes de informes');
  });
});