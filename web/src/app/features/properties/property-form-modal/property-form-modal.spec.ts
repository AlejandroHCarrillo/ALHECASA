import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { UserRole } from '../../../core/models/user-role';
import { Property } from '../property.models';
import { PropertyFormModal } from './property-form-modal';

describe('PropertyFormModal', () => {
  let http: HttpTestingController;

  const agencies = [{ id: 'agency-1', name: 'Demo', email: 'd@d.com', phone: null, isActive: true }];

  const existing: Property = {
    id: 'p-1',
    agencyId: 'agency-1',
    agencyName: 'Demo',
    title: 'Casa original',
    description: null,
    type: 'House',
    operation: 'Sale',
    status: 'Draft',
    price: 1_000_000,
    currency: 'MXN',
    street: null,
    neighborhood: null,
    city: 'CDMX',
    state: 'CDMX',
    postalCode: null,
    bedrooms: 2,
    bathrooms: null,
    constructionArea: null,
    landArea: null,
    imageIds: ['img-1'],
    createdAt: new Date().toISOString(),
    updatedAt: null,
  };

  const open = async (role: UserRole, property: Property | null = null): Promise<ComponentFixture<PropertyFormModal>> => {
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

    const fixture = TestBed.createComponent(PropertyFormModal);
    fixture.componentRef.setInput('agencies', agencies);
    fixture.componentRef.setInput('property', property);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  };

  const set = (fixture: ComponentFixture<PropertyFormModal>, id: string, value: string): void => {
    const element = fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  };

  const image = (name = 'foto.png', type = 'image/png'): File => new File(['x'], name, { type });

  const pick = (fixture: ComponentFixture<PropertyFormModal>, files: File[]): void => {
    const input = fixture.nativeElement.querySelector('#images') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: files, configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const fillRequiredWithoutImage = (fixture: ComponentFixture<PropertyFormModal>): void => {
    set(fixture, 'title', '  Casa en Polanco ');
    set(fixture, 'price', '4500000');
    set(fixture, 'city', 'Ciudad de México');
    set(fixture, 'state', 'CDMX');
    fixture.detectChanges();
  };

  const fillRequired = (fixture: ComponentFixture<PropertyFormModal>): void => {
    fillRequiredWithoutImage(fixture);
    pick(fixture, [image()]);
  };

  const imagesUrl = (id: string): string => `${environment.apiUrl}/properties/${id}/images`;

  const submit = (fixture: ComponentFixture<PropertyFormModal>): void => {
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

  it('does not send anything while the required fields are missing', async () => {
    const fixture = await open(UserRole.Seller);

    submit(fixture);

    http.expectNone(`${environment.apiUrl}/properties`);
    expect(fixture.nativeElement.textContent).toContain('El título es obligatorio.');
    expect(fixture.nativeElement.textContent).toContain('La ciudad es obligatoria.');
  });

  it('rejects a price of zero', async () => {
    const fixture = await open(UserRole.Seller);
    fillRequired(fixture);
    set(fixture, 'price', '0');
    fixture.detectChanges();

    submit(fixture);

    http.expectNone(`${environment.apiUrl}/properties`);
    expect(fixture.nativeElement.textContent).toContain('Captura un precio mayor a cero.');
  });

  it('creates a property for the own agency: no agency selector, optional fields as null', async () => {
    const fixture = await open(UserRole.Seller);
    expect(fixture.nativeElement.querySelector('#agencyId')).toBeNull();
    fillRequired(fixture);

    submit(fixture);

    const request = http.expectOne(`${environment.apiUrl}/properties`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      agencyId: null,
      title: 'Casa en Polanco',
      description: null,
      type: 'House',
      operation: 'Sale',
      status: 'Draft',
      price: 4_500_000,
      currency: 'MXN',
      street: null,
      neighborhood: null,
      city: 'Ciudad de México',
      state: 'CDMX',
      postalCode: null,
      bedrooms: null,
      bathrooms: null,
      constructionArea: null,
      landArea: null,
    });
  });

  it('sends the selected operation, type and currency', async () => {
    const fixture = await open(UserRole.AgencyAdmin);
    fillRequired(fixture);
    set(fixture, 'type', 'Apartment');
    set(fixture, 'operation', 'Rent');
    set(fixture, 'currency', 'USD');
    set(fixture, 'bedrooms', '3');
    fixture.detectChanges();

    submit(fixture);

    expect(http.expectOne(`${environment.apiUrl}/properties`).request.body).toMatchObject({
      type: 'Apartment',
      operation: 'Rent',
      currency: 'USD',
      bedrooms: 3,
    });
  });

  // --- Imágenes ---------------------------------------------------------------------------

  it('requires at least one image', async () => {
    const fixture = await open(UserRole.Seller);
    fillRequiredWithoutImage(fixture);

    submit(fixture);

    http.expectNone(`${environment.apiUrl}/properties`);
    expect(fixture.nativeElement.textContent).toContain('Agrega al menos una imagen.');
  });

  it('shows the staged images, the count and marks the first as cover; they can be removed', async () => {
    const fixture = await open(UserRole.Seller);

    pick(fixture, [image('a.png'), image('b.jpg', 'image/jpeg')]);

    expect(fixture.nativeElement.querySelector('[data-testid="image-count"]').textContent).toContain('2 de 10');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="image-tiles"] li').length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Portada');

    (fixture.nativeElement.querySelector('button[aria-label="Quitar imagen"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('[data-testid="image-tiles"] li').length).toBe(1);
  });

  it('rejects files that are not JPG, PNG or WebP, and files over 5 MB', async () => {
    const fixture = await open(UserRole.Seller);
    const big = new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'enorme.png', { type: 'image/png' });

    pick(fixture, [image('nota.pdf', 'application/pdf'), image('animada.gif', 'image/gif'), big]);

    expect(fixture.nativeElement.querySelector('[data-testid="image-tiles"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('"nota.pdf" no es una imagen JPG, PNG o WebP.');
    expect(fixture.nativeElement.textContent).toContain('"animada.gif" no es una imagen JPG, PNG o WebP.');
    expect(fixture.nativeElement.textContent).toContain('"enorme.png" pesa más de 5 MB.');
  });

  it('accepts at most 10 images and hides the picker when full', async () => {
    const fixture = await open(UserRole.Seller);

    pick(fixture, Array.from({ length: 12 }, (_, i) => image(`foto${i}.png`)));

    expect(fixture.nativeElement.querySelectorAll('[data-testid="image-tiles"] li').length).toBe(10);
    expect(fixture.nativeElement.textContent).toContain('no se agregó: el máximo es de 10 imágenes.');
    expect(fixture.nativeElement.querySelector('#images')).toBeNull();
  });

  it('creates as draft, uploads the images and, with another status, publishes at the end', async () => {
    const fixture = await open(UserRole.AgencyAdmin);
    fillRequired(fixture);
    pick(fixture, [image('segunda.jpg', 'image/jpeg')]);
    set(fixture, 'status', 'Published');
    fixture.detectChanges();
    const saved = vi.fn();
    fixture.componentInstance.saved.subscribe(saved);

    submit(fixture);

    const create = http.expectOne(`${environment.apiUrl}/properties`);
    expect(create.request.body).toMatchObject({ status: 'Draft' });
    create.flush({ ...existing, id: 'new-1', imageIds: [] });

    const upload = http.expectOne(imagesUrl('new-1'));
    expect(upload.request.method).toBe('POST');
    const body = upload.request.body as FormData;
    expect((body.getAll('files') as File[]).map((f) => f.name)).toEqual(['foto.png', 'segunda.jpg']);
    expect(body.getAll('removeImageIds')).toEqual([]);
    expect(saved).not.toHaveBeenCalled();
    upload.flush({ ...existing, id: 'new-1', imageIds: ['i1', 'i2'] });

    const publish = http.expectOne(`${environment.apiUrl}/properties/new-1`);
    expect(publish.request.method).toBe('PUT');
    expect(publish.request.body).toMatchObject({ status: 'Published' });
    publish.flush({ ...existing, id: 'new-1', status: 'Published' });

    expect(saved).toHaveBeenCalledWith('Propiedad creada correctamente.');
  });

  it('keeps what was saved when the upload fails and, on retry, does not create the property again', async () => {
    const fixture = await open(UserRole.Seller);
    fillRequired(fixture);

    submit(fixture);
    http.expectOne(`${environment.apiUrl}/properties`).flush({ ...existing, id: 'new-1', imageIds: [] });
    http.expectOne(imagesUrl('new-1')).flush(
      { title: 'Validation failed', detail: 'La imagen no es válida.' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();

    submit(fixture);

    http.expectNone(`${environment.apiUrl}/properties`);
    // Es un borrador: primero los datos y luego las imágenes pendientes.
    http.expectOne(`${environment.apiUrl}/properties/new-1`).flush({ ...existing, id: 'new-1' });
    http.expectOne(imagesUrl('new-1')).flush({ ...existing, id: 'new-1', imageIds: ['i1'] });
  });

  it('when editing and publishing, sends the image changes first and then the data', async () => {
    const fixture = await open(UserRole.Seller, existing);
    (fixture.nativeElement.querySelector('button[aria-label="Quitar imagen"]') as HTMLButtonElement).click();
    pick(fixture, [image('nueva.png')]);
    set(fixture, 'status', 'Published');
    fixture.detectChanges();

    submit(fixture);

    http.expectNone(`${environment.apiUrl}/properties/p-1`);
    const upload = http.expectOne(imagesUrl('p-1'));
    const body = upload.request.body as FormData;
    expect(body.getAll('removeImageIds')).toEqual(['img-1']);
    expect((body.getAll('files') as File[]).map((f) => f.name)).toEqual(['nueva.png']);
    upload.flush({ ...existing, imageIds: ['img-2'] });

    expect(http.expectOne(`${environment.apiUrl}/properties/p-1`).request.method).toBe('PUT');
  });

  it('does not call the images endpoint when the gallery did not change', async () => {
    const fixture = await open(UserRole.Seller, existing);

    submit(fixture);

    http.expectOne(`${environment.apiUrl}/properties/p-1`).flush(existing);
    http.expectNone(imagesUrl('p-1'));
  });

  it('does not allow removing every image when editing', async () => {
    const fixture = await open(UserRole.Seller, existing);
    (fixture.nativeElement.querySelector('button[aria-label="Quitar imagen"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    submit(fixture);

    http.expectNone(`${environment.apiUrl}/properties/p-1`);
    expect(fixture.nativeElement.textContent).toContain('Agrega al menos una imagen.');
  });

  it('requires the agency for a platform administrator, and sends it', async () => {
    const fixture = await open(UserRole.PlatformAdmin);
    fillRequired(fixture);

    submit(fixture);
    http.expectNone(`${environment.apiUrl}/properties`);

    set(fixture, 'agencyId', 'agency-1');
    fixture.detectChanges();
    submit(fixture);

    expect(http.expectOne(`${environment.apiUrl}/properties`).request.body).toMatchObject({ agencyId: 'agency-1' });
  });

  it('edits with PUT, loads the current values and never asks for the agency', async () => {
    const fixture = await open(UserRole.PlatformAdmin, existing);

    expect(fixture.nativeElement.querySelector('#agencyId')).toBeNull();
    expect((fixture.nativeElement.querySelector('#title') as HTMLInputElement).value).toBe('Casa original');
    expect((fixture.nativeElement.querySelector('#bedrooms') as HTMLInputElement).value).toBe('2');

    set(fixture, 'title', 'Casa editada');
    fixture.detectChanges();
    submit(fixture);

    const request = http.expectOne(`${environment.apiUrl}/properties/p-1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toMatchObject({ title: 'Casa editada', price: 1_000_000 });
    expect(request.request.body).not.toHaveProperty('agencyId');
  });

  it('shows the server error and keeps the form open', async () => {
    const fixture = await open(UserRole.Seller);
    fillRequired(fixture);

    submit(fixture);
    http.expectOne(`${environment.apiUrl}/properties`).flush(
      { title: 'Validation failed', errors: { Price: ['Price must be positive.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Price must be positive.');
  });
});
