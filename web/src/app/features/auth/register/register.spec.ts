import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Register } from './register';

const URL = `${environment.apiUrl}/auth/register`;

describe('Register', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<Register>;
  let router: Router;

  const create = (): void => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'propiedades', children: [] }]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(Register);
    fixture.detectChanges();
  };

  const type = (id: string, value: string): void => {
    const input = fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const fill = (password = 'Correct-Horse-9', confirm = password): void => {
    type('firstName', ' Ana ');
    type('lastName', 'Lopez');
    type('email', 'ana@demo.com');
    type('password', password);
    type('confirmPassword', confirm);
    fixture.detectChanges();
  };
  const submit = (): void => {
    (fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  };
  const text = () => fixture.nativeElement.textContent as string;

  const session = {
    accessToken: 'jwt',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    user: { id: '1', fullName: 'Ana Lopez', email: 'ana@demo.com', role: 'AgencyAdmin', agencyId: 'x', agencyName: 'Ana Lopez', isIndividual: true },
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('does not send an empty form and tells what is missing', () => {
    create();
    submit();

    http.expectNone(URL);
    expect(text()).toContain('El nombre es obligatorio.');
    expect(text()).toContain('Los apellidos son obligatorios.');
    expect(text()).toContain('Captura un correo válido.');
  });

  it.each(['corta1A', 'sinmayusculas123', 'SINMINUSCULAS123', 'SinNumeroAbcdef'])('rejects the weak password %s', (password) => {
    create();
    fill(password);
    submit();

    http.expectNone(URL);
    expect(fixture.nativeElement.querySelector('#password-help')?.classList.contains('text-error')).toBe(true);
  });

  it('does not send mismatching passwords', () => {
    create();
    fill('Correct-Horse-9', 'Correct-Horse-8');
    submit();

    http.expectNone(URL);
    expect(text()).toContain('Las contraseñas no coinciden.');
  });

  it('creates the account with trimmed data, without the confirmation, and opens the properties', () => {
    create();
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    fill();
    submit();

    const request = http.expectOne(URL);
    expect(request.request.body).toEqual({ firstName: 'Ana', lastName: 'Lopez', email: 'ana@demo.com', password: 'Correct-Horse-9' });
    request.flush(session, { status: 201, statusText: 'Created' });

    expect(localStorage.getItem('alhecasa.session')).toContain('jwt');
    expect(navigate).toHaveBeenCalledWith('/propiedades');
  });

  it.each([
    [409, 'El correo ya está registrado.'],
    [429, 'demasiadas cuentas'],
    [400, 'Revisa los datos capturados: Email invalid.'],
  ])('explains a %i and keeps what was typed', (status, expected) => {
    create();
    fill();
    submit();

    http.expectOne(URL).flush(status === 400 ? { errors: { Email: ['Email invalid.'] } } : null, { status, statusText: 'x' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-error-alert')?.textContent).toContain(expected);
    expect((fixture.nativeElement.querySelector('#email') as HTMLInputElement).value).toBe('ana@demo.com');
    expect(localStorage.getItem('alhecasa.session')).toBeNull();
  });

  it('links to the login for people who already have an account', () => {
    create();

    expect(fixture.nativeElement.querySelector('a[href="/login"]')).not.toBeNull();
  });

it('offers the privacy notice in a modal without submitting the form', () => {
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    create();
    fill();

    expect(fixture.nativeElement.querySelector('[data-testid="privacy-legend"]')?.textContent).toContain('crear tu cuenta');
    (fixture.nativeElement.querySelector('[data-testid="privacy-open"]') as HTMLElement).click();
    fixture.detectChanges();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="privacy-notice"]')).not.toBeNull();
    http.expectNone(URL);
  });
});
