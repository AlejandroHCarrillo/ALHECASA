import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { sampleInquiry } from '../inquiry.fixtures';
import { Inquiry } from '../inquiry.models';
import { InquiryDetailModal } from './inquiry-detail-modal';

const URL = `${environment.apiUrl}/inquiries/q-1`;

describe('InquiryDetailModal', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<InquiryDetailModal>;
  let updates: Inquiry[];

  const open = (inquiry: Inquiry = sampleInquiry()): void => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InquiryDetailModal);
    fixture.componentRef.setInput('inquiry', inquiry);
    updates = [];
    fixture.componentInstance.updated.subscribe((i) => updates.push(i));
    fixture.detectChanges();
  };

  const q = (selector: string) => fixture.nativeElement.querySelector(selector) as HTMLElement | null;
  const text = () => fixture.nativeElement.textContent as string;
  const write = (value: string): void => {
    const area = q('#inquiry-text') as HTMLTextAreaElement;
    area.value = value;
    area.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const save = (): void => {
    (q('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('shows the prospect, the property and the full question, with contact links', () => {
    open();

    expect(text()).toContain('Ana Lopez');
    expect(text()).toContain('Casa con alberca');
    expect(q('[data-testid="message"]')?.textContent).toContain('Puedo visitarla el sabado?');
    expect(q('[data-testid="phone-link"]')?.getAttribute('href')).toBe('tel:55 1234 5678');
    expect(q('[data-testid="email-link"]')?.getAttribute('href')).toBe('mailto:ana@demo.com');
    expect(q('a[href="/publicaciones/p-1"]')).not.toBeNull();
  });

  it('shows the follow-up history, replies apart from notes', () => {
    open(
      sampleInquiry({
        status: 'InProgress',
        followUps: [
          { id: 'f-1', kind: 'Reply', text: 'Si, sigue disponible', authorName: 'Vendedor Demo', createdAt: '2026-11-24T11:00:00Z' },
          { id: 'f-2', kind: 'Note', text: 'Pidio descuento', authorName: 'Admin Demo', createdAt: '2026-11-24T12:00:00Z' },
        ],
      }),
    );

    const items = fixture.nativeElement.querySelectorAll('[data-testid="follow-ups"] li');
    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('Respuesta al prospecto');
    expect(items[0].textContent).toContain('Vendedor Demo');
    expect(items[1].textContent).toContain('Nota interna');
    expect(q('[data-testid="no-follow-ups"]')).toBeNull();
  });

  it('says there is no follow-up yet', () => {
    open();

    expect(q('[data-testid="no-follow-ups"]')).not.toBeNull();
  });

  it('does not save an empty text', () => {
    open();
    write('   ');
    save();

    http.expectNone(`${URL}/follow-ups`);
    expect(text()).toContain('Escribe el texto.');
  });

  it('records a reply, emits the updated inquiry and clears the text', () => {
    open();
    write('  Le llame y agendamos visita  ');
    save();

    const request = http.expectOne(`${URL}/follow-ups`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ kind: 'Reply', text: 'Le llame y agendamos visita' });

    const updated = sampleInquiry({
      status: 'InProgress',
      followUps: [{ id: 'f-1', kind: 'Reply', text: 'Le llame y agendamos visita', authorName: 'Vendedor', createdAt: '2026-11-24T11:00:00Z' }],
    });
    request.flush(updated);
    fixture.detectChanges();

    expect(updates).toEqual([updated]);
    expect((q('#inquiry-text') as HTMLTextAreaElement).value).toBe('');
  });

  it('records an internal note when that kind is selected', () => {
    open();
    const note = fixture.nativeElement.querySelectorAll('input[type="radio"]')[1] as HTMLInputElement;
    note.click();
    fixture.detectChanges();
    write('Pidio descuento');
    save();

    expect(http.expectOne(`${URL}/follow-ups`).request.body).toEqual({ kind: 'Note', text: 'Pidio descuento' });
  });

  it('offers a prefilled email only for replies', () => {
    open();
    write('Hola Ana');

    const href = q('[data-testid="mailto-reply"]')?.getAttribute('href') ?? '';
    expect(href).toContain('mailto:ana@demo.com');
    expect(href).toContain(encodeURIComponent('Informes: Casa con alberca'));
    expect(href).toContain(encodeURIComponent('Hola Ana'));

    (fixture.nativeElement.querySelectorAll('input[type="radio"]')[1] as HTMLInputElement).click();
    fixture.detectChanges();
    expect(q('[data-testid="mailto-reply"]')).toBeNull();
  });

  it('changes the status', () => {
    open();
    const select = q('#inquiry-status') as HTMLSelectElement;
    select.value = 'Closed';
    select.dispatchEvent(new Event('change'));

    const request = http.expectOne(`${URL}/status`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ status: 'Closed' });
    const updated = sampleInquiry({ status: 'Closed' });
    request.flush(updated);

    expect(updates).toEqual([updated]);
  });

  it('does not call the API when the selected status is the current one', () => {
    open();
    const select = q('#inquiry-status') as HTMLSelectElement;
    select.dispatchEvent(new Event('change'));

    http.expectNone(`${URL}/status`);
  });

  it('shows the error and keeps the typed text when saving fails', () => {
    open();
    write('Respuesta');
    save();

    http.expectOne(`${URL}/follow-ups`).flush(null, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(q('app-error-alert')?.textContent).toContain('No tienes permiso');
    expect((q('#inquiry-text') as HTMLTextAreaElement).value).toBe('Respuesta');
    expect(updates).toHaveLength(0);
  });
});