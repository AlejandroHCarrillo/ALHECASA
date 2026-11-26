import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { UserRole } from '../../core/models/user-role';
import { AccountBilling } from '../billing/billing.models';
import { AgencyDashboard } from './dashboard.models';
import { Home } from './home';

const ME = `${environment.apiUrl}/billing/me`;

const DASHBOARD = `${environment.apiUrl}/dashboard`;

const dashboard = (overrides: Partial<AgencyDashboard> = {}): AgencyDashboard => ({
  properties: { total: 12, published: 6, draft: 2, paused: 1, closed: 3, featured: 2 },
  inquiries: { new: 4, inProgress: 2, closed: 5, receivedRecently: 3, total: 11 },
  recentDays: 7,
  latestNewInquiries: [
    { id: 'q-1', propertyId: 'p-1', propertyTitle: 'Casa en Polanco', name: 'Ana López', createdAt: '2026-11-25T17:30:00Z' },
    { id: 'q-2', propertyId: 'p-2', propertyTitle: 'Depto en Roma', name: 'Luis Pérez', createdAt: '2026-11-24T10:00:00Z' },
  ],
  ...overrides,
});

const account = (overrides: Partial<AccountBilling> = {}): AccountBilling => ({
  agencyId: 'agency-1',
  agencyName: 'Demo',
  isIndividual: false,
  listings: { planName: 'Inmobiliaria 100', limit: 100, used: 40, endsAt: '2026-12-25T12:00:00Z', isFree: false },
  featured: { planName: '5 destacados', limit: 5, used: 3, endsAt: '2026-12-20T12:00:00Z' },
  ...overrides,
});

describe('Home: plan de la cuenta', () => {
  let http: HttpTestingController;

  const open = (role: UserRole): ComponentFixture<Home> => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Me', email: 'me@a.com', role, agencyId: role === UserRole.PlatformAdmin ? null : 'agency-1', agencyName: 'Demo' },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    return fixture;
  };

  const text = (fixture: ComponentFixture<Home>, id: string): string | undefined =>
    (fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null)?.textContent?.trim();

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('shows the account administrator what the account has, with a link to buy', () => {
    const fixture = open(UserRole.AgencyAdmin);
    http.expectOne(ME).flush(account());
    fixture.detectChanges();

    expect(text(fixture, 'listings-plan')).toContain('Inmobiliaria 100');
    expect(text(fixture, 'listings-usage')).toContain('40 de 100');
    expect(text(fixture, 'listings-usage')).toContain('25/12/2026');
    expect(text(fixture, 'featured-plan')).toBe('5 destacados');
    expect(text(fixture, 'featured-usage')).toContain('3 de 5');
    const link = fixture.nativeElement.querySelector('[data-testid="buy-plan"]') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/mi-plan');
  });

  it.each([UserRole.Supervisor, UserRole.Seller])('shows %s the plan but does not offer to buy it', (role) => {
    const fixture = open(role);
    http.expectOne(ME).flush(account());
    fixture.detectChanges();

    expect(text(fixture, 'listings-plan')).toContain('Inmobiliaria 100');
    expect(fixture.nativeElement.querySelector('[data-testid="buy-plan"]')).toBeNull();
  });

  it('shows the free plan of an individual account', () => {
    const fixture = open(UserRole.AgencyAdmin);
    http.expectOne(ME).flush(
      account({ isIndividual: true, listings: { planName: 'Gratuito', limit: 3, used: 3, endsAt: null, isFree: true }, featured: null }),
    );
    fixture.detectChanges();

    expect(text(fixture, 'listings-plan')).toContain('Gratuito');
    expect(text(fixture, 'listings-usage')).toContain('3 de 3');
    expect(text(fixture, 'no-featured')).toBeDefined();
  });

  it('does not ask for a plan to the platform administrator, who has no account', () => {
    const fixture = open(UserRole.PlatformAdmin);
    http.expectNone(ME);

    expect(fixture.nativeElement.querySelector('[data-testid="listings-plan"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Bienvenido');
  });

  it('still welcomes the user when the plan cannot be loaded', () => {
    const fixture = open(UserRole.AgencyAdmin);
    http.expectOne(ME).flush({ title: 'boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Bienvenido');
    expect(fixture.nativeElement.querySelector('[data-testid="listings-plan"]')).toBeNull();
  });

  describe('resumen de actividad', () => {
    const value = (fixture: ComponentFixture<Home>, key: string): string | undefined => text(fixture, `value-${key}`);

    it('shows how many properties there are in each state, featured ones included', () => {
      const fixture = open(UserRole.AgencyAdmin);
      http.expectOne(DASHBOARD).flush(dashboard());
      http.expectOne(ME).flush(account());
      fixture.detectChanges();

      expect(value(fixture, 'published')).toBe('6');
      expect(value(fixture, 'featured')).toBe('2');
      expect(value(fixture, 'draft')).toBe('2');
      expect(value(fixture, 'paused')).toBe('1');
      expect(value(fixture, 'closed')).toBe('3');
      expect(text(fixture, 'properties-total')).toContain('12 en total');
    });

    it('shows the inquiries by stage and how many arrived recently', () => {
      const fixture = open(UserRole.Seller);
      http.expectOne(DASHBOARD).flush(dashboard());
      http.expectOne(ME).flush(account());
      fixture.detectChanges();

      expect(value(fixture, 'inquiry-new')).toBe('4');
      expect(value(fixture, 'inquiry-progress')).toBe('2');
      expect(value(fixture, 'inquiry-closed')).toBe('5');
      expect(value(fixture, 'inquiry-recent')).toBe('3');
      expect(text(fixture, 'stat-inquiry-recent')).toContain('7 días');
    });

    it('lists the latest new questions with who asked and about which property', () => {
      const fixture = open(UserRole.AgencyAdmin);
      http.expectOne(DASHBOARD).flush(dashboard());
      http.expectOne(ME).flush(account());
      fixture.detectChanges();

      const rows = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="latest-inquiry"]') as NodeListOf<HTMLElement>);
      expect(rows).toHaveLength(2);
      expect(rows[0].textContent).toContain('Ana López');
      expect(rows[0].textContent).toContain('Casa en Polanco');
      expect(rows[1].textContent).toContain('Luis Pérez');
    });

    it('says so when there are no new questions', () => {
      const fixture = open(UserRole.AgencyAdmin);
      http.expectOne(DASHBOARD).flush(dashboard({ latestNewInquiries: [], inquiries: { new: 0, inProgress: 0, closed: 0, receivedRecently: 0, total: 0 } }));
      http.expectOne(ME).flush(account());
      fixture.detectChanges();

      expect(text(fixture, 'no-new-inquiries')).toContain('No tienes preguntas nuevas');
      expect(value(fixture, 'inquiry-new')).toBe('0');
    });

    it('opens each list already filtered by the card that was pressed', () => {
      const fixture = open(UserRole.AgencyAdmin);
      http.expectOne(DASHBOARD).flush(dashboard());
      http.expectOne(ME).flush(account());
      fixture.detectChanges();

      const href = (key: string) => (fixture.nativeElement.querySelector(`[data-testid="stat-${key}"]`) as HTMLAnchorElement).getAttribute('href');
      expect(href('published')).toBe('/propiedades?status=Published');
      expect(href('paused')).toBe('/propiedades?status=Paused');
      expect(href('closed')).toBe('/propiedades?status=Closed');
      expect(href('featured')).toBe('/propiedades');
      expect(href('inquiry-new')).toBe('/solicitudes?status=New');
      expect(href('inquiry-progress')).toBe('/solicitudes?status=InProgress');
      expect(href('inquiry-recent')).toBe('/solicitudes');
    });

    it('does not ask the platform administrator for a summary, who has no agency', () => {
      const fixture = open(UserRole.PlatformAdmin);
      http.expectNone(DASHBOARD);

      expect(fixture.nativeElement.querySelector('[data-testid="stat-published"]')).toBeNull();
    });

    it('still shows the plan when the summary cannot be loaded, and the other way round', () => {
      const fixture = open(UserRole.AgencyAdmin);
      http.expectOne(DASHBOARD).flush({ title: 'boom' }, { status: 500, statusText: 'Server Error' });
      http.expectOne(ME).flush(account());
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="stat-published"]')).toBeNull();
      expect(text(fixture, 'listings-plan')).toContain('Inmobiliaria 100');

      TestBed.resetTestingModule();
      const second = open(UserRole.AgencyAdmin);
      http.expectOne(DASHBOARD).flush(dashboard());
      http.expectOne(ME).flush({ title: 'boom' }, { status: 500, statusText: 'Server Error' });
      second.detectChanges();

      expect(value(second, 'published')).toBe('6');
      expect(second.nativeElement.querySelector('[data-testid="listings-plan"]')).toBeNull();
    });
  });
});

describe('Home: información de la inmobiliaria', () => {
  let http: HttpTestingController;
  const ME_AGENCY = `${environment.apiUrl}/agencies/me`;

  const open = (role: UserRole, isIndividual = false): ComponentFixture<Home> => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: {
          id: 'me', fullName: 'Me', email: 'me@a.com', role, agencyId: role === UserRole.PlatformAdmin ? null : 'agency-1', agencyName: 'Demo', isIndividual,
        },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('shows the administrator of an agency the card to edit its information', () => {
    const fixture = open(UserRole.AgencyAdmin);
    http.expectOne(ME_AGENCY).flush({
      id: 'agency-1', name: 'Demo', email: 'demo@a.com', phone: null, description: null, website: null, facebookUrl: null, instagramUrl: null,
      openingHours: null, street: null, city: null, state: null, postalCode: null, hasLogo: false,
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="agency-profile"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="edit"]')).not.toBeNull();
  });

  it.each([UserRole.Supervisor, UserRole.Seller, UserRole.PlatformAdmin])('does not offer %s to edit the agency, nor ask for it', (role) => {
    const fixture = open(role);

    http.expectNone(ME_AGENCY);
    expect(fixture.nativeElement.querySelector('app-agency-profile-card')).toBeNull();
  });

  it('does not offer a free individual account an agency profile', () => {
    const fixture = open(UserRole.AgencyAdmin, true);

    http.expectNone(ME_AGENCY);
    expect(fixture.nativeElement.querySelector('app-agency-profile-card')).toBeNull();
  });
});
