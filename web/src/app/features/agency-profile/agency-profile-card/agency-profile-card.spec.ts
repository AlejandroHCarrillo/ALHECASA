import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../shared/services/toast.service';
import { AgencyProfile } from '../agency-profile.models';
import { AgencyProfileCard } from './agency-profile-card';

const ME = `${environment.apiUrl}/agencies/me`;

const profile = (overrides: Partial<AgencyProfile> = {}): AgencyProfile => ({
  id: 'agency-1',
  name: 'Inmobiliaria Norte',
  email: 'contacto@norte.test',
  phone: '55 1234 5678',
  description: 'Casas y departamentos.',
  website: null,
  facebookUrl: null,
  instagramUrl: null,
  openingHours: 'Lun a Vie 9 a 18',
  street: 'Av. Reforma 100',
  city: 'Ciudad de México',
  state: 'CDMX',
  postalCode: '06600',
  hasLogo: false,
  ...overrides,
});

describe('AgencyProfileCard', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<AgencyProfileCard>;

  const open = (): void => {
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 'me', fullName: 'Ana', email: 'ana@a.com', role: 'AgencyAdmin', agencyId: 'agency-1', agencyName: 'Inmobiliaria Norte' },
      }),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AgencyProfileCard);
    fixture.detectChanges();
  };

  const byId = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const text = (id: string) => byId(id)?.textContent?.trim();

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('shows nothing until the profile arrives, and nothing if it cannot be loaded', () => {
    open();
    expect(byId('agency-profile')).toBeNull();

    http.expectOne(ME).flush(null, { status: 500, statusText: 'x' });
    fixture.detectChanges();

    expect(byId('agency-profile')).toBeNull();
  });

  it('shows the information the agency configured, and a link to its public page', () => {
    open();
    http.expectOne(ME).flush(profile());
    fixture.detectChanges();

    expect(text('name')).toBe('Inmobiliaria Norte');
    expect(text('email')).toBe('contacto@norte.test');
    expect(text('phone')).toBe('55 1234 5678');
    expect(text('address')).toBe('Av. Reforma 100, Ciudad de México, CDMX, 06600');
    expect(text('hours')).toBe('Lun a Vie 9 a 18');
    expect(byId('incomplete')).toBeNull();
    expect(byId('logo')).toBeNull();
    expect(byId('public-page')?.getAttribute('href')).toBe('/inmobiliarias/agency-1');
  });

  it('says what is missing and nudges to complete the profile when it is empty', () => {
    open();
    http.expectOne(ME).flush(profile({ phone: null, description: null, street: null, city: null, state: null, postalCode: null, openingHours: null }));
    fixture.detectChanges();

    expect(text('phone')).toBe('Sin capturar');
    expect(text('address')).toBe('Sin capturar');
    expect(byId('incomplete')?.textContent).toContain('Completa la información');
    expect(byId('hours')).toBeNull();
  });

  it('shows the logo when there is one', () => {
    open();
    http.expectOne(ME).flush(profile({ hasLogo: true }));
    fixture.detectChanges();

    expect(byId('logo')?.getAttribute('src')).toContain('/public/agencies/agency-1/logo');
  });

  it('edits in a modal and refreshes the card, the session name and tells the user', () => {
    open();
    http.expectOne(ME).flush(profile());
    fixture.detectChanges();
    const toast = TestBed.inject(ToastService);
    const success = vi.spyOn(toast, 'success');

    expect(fixture.nativeElement.querySelector('app-agency-profile-modal')).toBeNull();
    byId('edit')!.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-agency-profile-modal')).not.toBeNull();

    const name = fixture.nativeElement.querySelector('#agency-name') as HTMLInputElement;
    name.value = 'Norte Renovada';
    name.dispatchEvent(new Event('input'));
    byId('save')!.click();
    fixture.detectChanges();

    const update = http.expectOne(ME);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.name).toBe('Norte Renovada');
    update.flush(profile({ name: 'Norte Renovada' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-agency-profile-modal')).toBeNull();
    expect(text('name')).toBe('Norte Renovada');
    expect(TestBed.inject(AuthService).user()?.agencyName).toBe('Norte Renovada');
    expect(success).toHaveBeenCalled();
  });

  it('closes the modal without changes on cancel', () => {
    open();
    http.expectOne(ME).flush(profile());
    fixture.detectChanges();

    byId('edit')!.click();
    fixture.detectChanges();
    (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]).find((b) => b.textContent?.trim() === 'Cancelar')!.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-agency-profile-modal')).toBeNull();
    expect(text('name')).toBe('Inmobiliaria Norte');
  });
});