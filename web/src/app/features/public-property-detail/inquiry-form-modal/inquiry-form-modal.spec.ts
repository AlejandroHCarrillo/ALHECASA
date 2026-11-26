import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { InquiryFormModal, phoneValidator } from './inquiry-form-modal';

const URL = `${environment.apiUrl}/public/properties/p-1/inquiries`;

describe('phoneValidator', () => {
  it.each(['5512345678', '+52 55 1234 5678', '(55) 1234-5678', '55.1234.5678'])('accepts %s', (value) => {
    expect(phoneValidator(new FormControl(value, { nonNullable: true }))).toBeNull();
  });

  it.each(['123456', '1234567890123456', 'abc12345678', '55-1234-ext5', '++5512345678'])('rejects %s', (value) => {
    expect(phoneValidator(new FormControl(value, { nonNullable: true }))).toEqual({ phone: true });
  });

  it('leaves the empty value to the required validator', () => {
    expect(phoneValidator(new FormControl('', { nonNullable: true }))).toBeNull();
  });
});

describe('InquiryFormModal', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<InquiryFormModal>;

  const open = (session = false): void => {
    if (session) {
      localStorage.setItem(
        'alhecasa.session',
        JSON.stringify({
          token: 'jwt',
          expiresAt: new Date(Date.now() + 60_000).toISOString(),
          user: { id: 'me', fullName: 'Ana Lopez', email: 'ana@demo.com', role: 'Client', agencyId: null, agencyName: null },
        }),
      );
    }

    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InquiryFormModal);
    fixture.componentRef.setInput('propertyId', 'p-1');
    fixture.componentRef.setInput('propertyTitle', 'Casa con alberca');
    fixture.detectChanges();
  };

  const field = (id: string) => fixture.nativeElement.querySelector(`#inquiry-${id}`) as HTMLInputElement;
  const type = (id: string, value: string): void => {
    field(id).value = value;
    field(id).dispatchEvent(new Event('input'));
  };
  const fill = (): void => {
    type('name', '  Ana Lopez ');
    type('phone', '55 1234 5678');
    type('email', 'ana@demo.com');
    type('message', 'Sigue disponible?');
    fixture.detectChanges();
  };
  const submit = (): void => {
    (fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  };
  const alertText = () => fixture.nativeElement.querySelector('app-error-alert')?.textContent as string | undefined;

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('names the property and does not send an empty form', () => {
    open();

    expect(fixture.nativeElement.textContent).toContain('Casa con alberca');
    submit();

    http.expectNone(URL);
    expect(fixture.nativeElement.textContent).toContain('El nombre es obligatorio.');
    expect(fixture.nativeElement.textContent).toContain('Captura un teléfono válido');
    expect(fixture.nativeElement.textContent).toContain('Captura un correo válido.');
    expect(fixture.nativeElement.textContent).toContain('Escribe tu pregunta.');
  });

  it('does not send a malformed phone or email', () => {
    open();
    fill();
    type('phone', '123');
    type('email', 'no-es-correo');
    fixture.detectChanges();
    submit();

    http.expectNone(URL);
  });

  it('posts the trimmed data without a token and shows the confirmation', () => {
    open();
    fill();
    submit();

    const request = http.expectOne(URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.has('Authorization')).toBe(false);
    expect(request.request.body).toEqual({
      name: 'Ana Lopez',
      phone: '55 1234 5678',
      email: 'ana@demo.com',
      message: 'Sigue disponible?',
    });
    request.flush(null, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="sent"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('form[novalidate]')).toBeNull();
  });

  it('closes from the confirmation', () => {
    open();
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);
    fill();
    submit();
    http.expectOne(URL).flush(null, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="sent"] button') as HTMLButtonElement).click();

    expect(closed).toBe(1);
  });

  it('suggests name and email when there is a session', () => {
    open(true);

    expect(field('name').value).toBe('Ana Lopez');
    expect(field('email').value).toBe('ana@demo.com');
    expect(field('phone').value).toBe('');
  });

  it.each([
    [404, 'Esta propiedad ya no está disponible.'],
    [429, 'Enviaste muchas solicitudes seguidas'],
    [400, 'Revisa los datos capturados: Teléfono inválido.'],
  ])('explains a %i and keeps what was typed', (status, expected) => {
    open();
    fill();
    submit();

    http.expectOne(URL).flush(status === 400 ? { errors: { Phone: ['Teléfono inválido.'] } } : null, { status, statusText: 'x' });
    fixture.detectChanges();

    expect(alertText()).toContain(expected);
    expect(field('name').value).toBe('  Ana Lopez ');
    expect(fixture.nativeElement.querySelector('[data-testid="sent"]')).toBeNull();
  });

it('offers the privacy notice in a modal without sending the request', () => {
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    open();
    fill();

    expect(fixture.nativeElement.querySelector('[data-testid="privacy-legend"]')?.textContent).toContain('enviar la solicitud');
    (fixture.nativeElement.querySelector('[data-testid="privacy-open"]') as HTMLElement).click();
    fixture.detectChanges();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="privacy-notice"]')).not.toBeNull();
    http.expectNone(URL);

    (fixture.nativeElement.querySelector('[data-testid="privacy-close"]') as HTMLElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="privacy-notice"]')).toBeNull();
    expect(field('name').value).toBe('  Ana Lopez '); // el formulario conserva lo capturado
  });
});
