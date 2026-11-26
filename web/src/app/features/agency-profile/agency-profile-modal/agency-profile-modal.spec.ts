import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { AgencyProfile } from '../agency-profile.models';
import { AgencyProfileModal, webAddressValidator } from './agency-profile-modal';

const ME = `${environment.apiUrl}/agencies/me`;
const LOGO = `${ME}/logo`;

const profile = (overrides: Partial<AgencyProfile> = {}): AgencyProfile => ({
  id: 'agency-1',
  name: 'Inmobiliaria Norte',
  email: 'contacto@norte.test',
  phone: null,
  description: null,
  website: null,
  facebookUrl: null,
  instagramUrl: null,
  openingHours: null,
  street: null,
  city: null,
  state: null,
  postalCode: null,
  hasLogo: false,
  ...overrides,
});

describe('webAddressValidator', () => {
  const check = (value: string) => webAddressValidator(new FormControl(value, { nonNullable: true }));

  it.each(['', '   ', 'http://norte.test', 'https://www.norte.test/contacto?x=1'])('accepts "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it.each(['javascript:alert(1)', 'data:text/html,hola', 'ftp://norte.test', 'norte.test', 'https://', '//norte.test'])(
    'rejects "%s"',
    (value) => {
      expect(check(value)).toEqual({ webAddress: true });
    },
  );
});

describe('AgencyProfileModal', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<AgencyProfileModal>;
  let saved: AgencyProfile[];
  let cancelled: number;

  const open = (current: AgencyProfile = profile()): void => {
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AgencyProfileModal);
    fixture.componentRef.setInput('profile', current);
    saved = [];
    cancelled = 0;
    fixture.componentInstance.saved.subscribe((p) => saved.push(p));
    fixture.componentInstance.cancelled.subscribe(() => cancelled++);
    fixture.detectChanges();
  };

  const field = (id: string) => fixture.nativeElement.querySelector(`#agency-${id}`) as HTMLInputElement;
  const type = (id: string, value: string): void => {
    field(id).value = value;
    field(id).dispatchEvent(new Event('input'));
  };
  const click = (testId: string): void => {
    (fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement).click();
    fixture.detectChanges();
  };
  const submit = (): void => click('save');
  const text = () => fixture.nativeElement.textContent as string;
  const pickLogo = (file: File): void => {
    const input = fixture.nativeElement.querySelector('[data-testid="logo-input"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };
  const png = (name = 'logo.png', size = 100) => new File([new Uint8Array(size)], name, { type: 'image/png' });

  it('opens with the current information', () => {
    open(profile({ phone: '55 1234 5678', description: 'Casas', website: 'https://norte.test', city: 'Puebla' }));

    expect(field('name').value).toBe('Inmobiliaria Norte');
    expect(field('email').value).toBe('contacto@norte.test');
    expect(field('phone').value).toBe('55 1234 5678');
    expect(field('description').value).toBe('Casas');
    expect(field('website').value).toBe('https://norte.test');
    expect(field('city').value).toBe('Puebla');
    expect(field('hours').value).toBe('');
  });

  it('tells that what is captured is public and offers the privacy notice', () => {
    open();

    expect(text()).toContain('se muestra en la página pública');
    expect(fixture.nativeElement.querySelector('[data-testid="privacy-legend"]')?.textContent).toContain('guardar');
  });

  it('requires a name and a valid email, and sends nothing', () => {
    open();
    type('name', '  ');
    type('email', 'no-es-correo');
    fixture.detectChanges();

    submit();

    expect(text()).toContain('El nombre es obligatorio.');
    expect(text()).toContain('Captura un correo válido.');
    http.expectNone(ME);
  });

  it.each(['javascript:alert(1)', 'norte.test', 'ftp://norte.test'])('rejects the address "%s" in the links', (url) => {
    open();
    type('website', url);
    fixture.detectChanges();

    submit();

    expect(text()).toContain('empiece con http:// o https://');
    http.expectNone(ME);
  });

  it('rejects an invalid phone', () => {
    open();
    type('phone', 'abc');
    fixture.detectChanges();

    submit();

    expect(text()).toContain('Captura un teléfono válido');
    http.expectNone(ME);
  });

  it('saves the data trimmed, sending empty optional fields as null, and reports the result', () => {
    open();
    type('name', '  Norte SA ');
    type('phone', '55 1234 5678');
    type('description', '  Casas y departamentos.  ');
    type('website', 'https://norte.test');
    type('street', '   ');
    type('city', 'Puebla');
    fixture.detectChanges();

    submit();

    const request = http.expectOne(ME);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Norte SA',
      email: 'contacto@norte.test',
      phone: '55 1234 5678',
      description: 'Casas y departamentos.',
      website: 'https://norte.test',
      facebookUrl: null,
      instagramUrl: null,
      openingHours: null,
      street: null,
      city: 'Puebla',
      state: null,
      postalCode: null,
    });
    const result = profile({ name: 'Norte SA', city: 'Puebla' });
    request.flush(result);
    fixture.detectChanges();

    expect(saved).toEqual([result]);
    http.expectNone(LOGO);
  });

  it('shows the server message and does not close when saving fails', () => {
    open();
    submit();

    http.expectOne(ME).flush({ errors: { Website: ['La dirección no es válida.'] } }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(text()).toContain('La dirección no es válida.');
    expect(saved).toEqual([]);
    expect(field('name').value).toBe('Inmobiliaria Norte');
  });

  it('uploads a new logo after saving the data, and reports the final profile', () => {
    open();
    pickLogo(png('mi-logo.png'));
    expect(fixture.nativeElement.querySelector('[data-testid="logo-preview"] img, [data-testid="logo-preview"] i')).not.toBeNull();

    submit();

    http.expectOne(ME).flush(profile());
    const upload = http.expectOne(LOGO);
    expect(upload.request.method).toBe('PUT');
    const form = upload.request.body as FormData;
    expect((form.get('file') as File).name).toBe('mi-logo.png');
    const final = profile({ hasLogo: true });
    upload.flush(final);

    expect(saved).toEqual([final]);
  });

  it('shows the saved logo and removes it when asked', () => {
    open(profile({ hasLogo: true }));
    expect(fixture.nativeElement.querySelector('[data-testid="logo-preview"] img')?.getAttribute('src')).toContain(
      '/public/agencies/agency-1/logo',
    );

    click('logo-remove');
    expect(fixture.nativeElement.querySelector('[data-testid="logo-preview"] img')).toBeNull();

    submit();
    http.expectOne(ME).flush(profile({ hasLogo: true }));
    const removal = http.expectOne(LOGO);
    expect(removal.request.method).toBe('DELETE');
    removal.flush(profile({ hasLogo: false }));

    expect(saved).toHaveLength(1);
    expect(saved[0].hasLogo).toBe(false);
  });

  it('does not touch the logo when nothing changed about it', () => {
    open(profile({ hasLogo: true }));

    submit();
    http.expectOne(ME).flush(profile({ hasLogo: true }));

    http.expectNone(LOGO);
    expect(saved).toHaveLength(1);
  });

  it('refuses a file that is not an image, or is too big, and keeps what was there', () => {
    open(profile({ hasLogo: true }));

    pickLogo(new File(['<svg/>'], 'logo.svg', { type: 'image/svg+xml' }));
    expect(fixture.nativeElement.querySelector('[data-testid="logo-problem"]')?.textContent).toContain('no es una imagen JPG, PNG o WebP');

    pickLogo(png('grande.png', 5 * 1024 * 1024 + 1));
    expect(fixture.nativeElement.querySelector('[data-testid="logo-problem"]')?.textContent).toContain('pesa más de 5 MB');

    submit();
    http.expectOne(ME).flush(profile({ hasLogo: true }));
    http.expectNone(LOGO);
  });

  it('closes without saving on cancel', () => {
    open();

    (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]).find((b) => b.textContent?.trim() === 'Cancelar')!.click();

    expect(cancelled).toBe(1);
    http.expectNone(ME);
  });
});