import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/models/user-role';
import { User } from '../user.models';
import { UserFormModal } from './user-form-modal';

describe('UserFormModal: roles del personal de la inmobiliaria', () => {
  let http: HttpTestingController;

  const agencies = [{ id: 'agency-1', name: 'Demo', email: 'd@d.com', phone: null, isActive: true }];

  const open = async (role: UserRole, user: User | null = null): Promise<ComponentFixture<UserFormModal>> => {
    localStorage.setItem(
      'alhecasa.session',
      JSON.stringify({
        token: 'jwt',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: {
          id: 'me',
          fullName: 'Me',
          email: 'me@a.com',
          role,
          agencyId: role === UserRole.PlatformAdmin ? null : 'agency-1',
          agencyName: null,
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(UserFormModal);
    fixture.componentRef.setInput('agencies', agencies);
    fixture.componentRef.setInput('user', user);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  };

  const roleOptions = (fixture: ComponentFixture<UserFormModal>): string[] =>
    Array.from(fixture.nativeElement.querySelectorAll('#role option') as NodeListOf<HTMLOptionElement>).map(
      (o) => o.value,
    );

  const select = (fixture: ComponentFixture<UserFormModal>, id: string, value: string): void => {
    const element = fixture.nativeElement.querySelector(`#${id}`) as HTMLSelectElement;
    element.value = value;
    element.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const type = (fixture: ComponentFixture<UserFormModal>, id: string, value: string): void => {
    const element = fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement;
    element.value = value;
    element.dispatchEvent(new Event('input'));
  };

  const fillPerson = (fixture: ComponentFixture<UserFormModal>): void => {
    type(fixture, 'firstName', 'Ana');
    type(fixture, 'lastName', 'Lopez');
    type(fixture, 'email', 'ana@demo.com');
    type(fixture, 'password', 'Correct-Horse-9');
    fixture.detectChanges();
  };

  const submit = (fixture: ComponentFixture<UserFormModal>): void => {
    (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  beforeEach(() => {
    localStorage.clear();
    // jsdom no implementa <dialog>.showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
  });

  afterEach(() => localStorage.clear());

  it('offers only staff roles to an agency administrator: no PlatformAdmin and no Client', async () => {
    const fixture = await open(UserRole.AgencyAdmin);

    expect(roleOptions(fixture)).toEqual(['AgencyAdmin', 'Supervisor', 'Seller']);
  });

  it('keeps the Client role visible when an agency administrator edits an existing client', async () => {
    const existingClient: User = {
      id: 'c-1',
      firstName: 'Cora',
      lastName: 'Cliente',
      fullName: 'Cora Cliente',
      email: 'cora@demo.com',
      role: UserRole.Client,
      agencyId: 'agency-1',
      agencyName: 'Demo',
      isActive: true,
      createdAt: new Date().toISOString(),
      lastLoginAt: null,
    };

    const fixture = await open(UserRole.AgencyAdmin, existingClient);

    expect(roleOptions(fixture)).toEqual(['AgencyAdmin', 'Supervisor', 'Seller', 'Client']);
    expect((fixture.nativeElement.querySelector('#role') as HTMLSelectElement).value).toBe('Client');
  });

  it('offers every role to a platform administrator', async () => {
    const fixture = await open(UserRole.PlatformAdmin);

    expect(roleOptions(fixture)).toEqual(['PlatformAdmin', 'AgencyAdmin', 'Supervisor', 'Seller', 'Client']);
  });

  it('suggests Seller by default to an agency administrator and hides the agency selector', async () => {
    const fixture = await open(UserRole.AgencyAdmin);

    expect((fixture.nativeElement.querySelector('#role') as HTMLSelectElement).value).toBe('Seller');
    expect(fixture.nativeElement.querySelector('#agencyId')).toBeNull();
  });

  it('creates a seller for the own agency without sending an agency id', async () => {
    const fixture = await open(UserRole.AgencyAdmin);
    fillPerson(fixture);

    submit(fixture);

    const request = http.expectOne(`${environment.apiUrl}/users`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({ role: 'Seller', email: 'ana@demo.com', agencyId: null });
  });

  it('creates a supervisor when selected', async () => {
    const fixture = await open(UserRole.AgencyAdmin);
    fillPerson(fixture);
    select(fixture, 'role', 'Supervisor');

    submit(fixture);

    expect(http.expectOne(`${environment.apiUrl}/users`).request.body).toMatchObject({ role: 'Supervisor' });
  });

  it.each(['Supervisor', 'Seller'])(
    'requires an agency when a platform administrator picks %s',
    async (role) => {
      const fixture = await open(UserRole.PlatformAdmin);
      fillPerson(fixture);
      select(fixture, 'role', role);

      submit(fixture);
      http.expectNone(`${environment.apiUrl}/users`);

      select(fixture, 'agencyId', 'agency-1');
      submit(fixture);

      expect(http.expectOne(`${environment.apiUrl}/users`).request.body).toMatchObject({
        role,
        agencyId: 'agency-1',
      });
    },
  );

  it('lets a platform administrator create a client without agency', async () => {
    const fixture = await open(UserRole.PlatformAdmin);
    fillPerson(fixture);
    select(fixture, 'role', 'Client');

    submit(fixture);

    expect(http.expectOne(`${environment.apiUrl}/users`).request.body).toMatchObject({
      role: 'Client',
      agencyId: null,
    });
  });
});
