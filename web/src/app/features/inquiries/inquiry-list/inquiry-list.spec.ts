import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { sampleInquiry } from '../inquiry.fixtures';
import { Inquiry } from '../inquiry.models';
import { InquiryList } from './inquiry-list';

const URL = `${environment.apiUrl}/inquiries`;

const page = (items: Inquiry[], total = items.length, pageNumber = 1) => ({
  items,
  page: pageNumber,
  pageSize: 10,
  totalCount: total,
  totalPages: Math.max(1, Math.ceil(total / 10)),
});

describe('InquiryList', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<InquiryList>;

  const create = (): void => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InquiryList);
    fixture.detectChanges();
  };

  const text = () => fixture.nativeElement.textContent as string;
  const list = () => http.expectOne((r) => r.url === URL);

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
    localStorage.clear();
  });

  it('asks for the first page with no filters', () => {
    create();

    const request = list();
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('10');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('search')).toBe(false);
    request.flush(page([]));
  });

  it.each(['New', 'InProgress', 'Closed'])('opens already filtered when the address carries ?status=%s (the home cards link here)', (status) => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ status }) } } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InquiryList);
    fixture.detectChanges();

    const request = list();
    expect(request.request.params.get('status')).toBe(status);
    request.flush(page([]));
  });

  it('ignores a ?status= that is not a stage', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ status: 'Hackeada' }) } } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(InquiryList);
    fixture.detectChanges();

    const request = list();
    expect(request.request.params.has('status')).toBe(false);
    request.flush(page([]));
  });

  it('lists the prospect, contact data, property, status and number of replies', () => {
    create();
    list().flush(
      page([
        sampleInquiry(),
        sampleInquiry({
          id: 'q-2',
          name: 'Luis Perez',
          status: 'InProgress',
          followUps: [{ id: 'f-1', kind: 'Reply', text: 'Hola', authorName: 'Vendedor', createdAt: '2026-11-24T11:00:00Z' }],
        }),
      ]),
    );
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Ana Lopez');
    expect(rows[0].textContent).toContain('55 1234 5678');
    expect(rows[0].textContent).toContain('ana@demo.com');
    expect(rows[0].textContent).toContain('Casa con alberca');
    expect(rows[0].textContent).toContain('Nueva');
    expect(rows[1].textContent).toContain('En seguimiento');
  });

  it('says when there are no inquiries', () => {
    create();
    list().flush(page([]));
    fixture.detectChanges();

    expect(text()).toContain('No se encontraron solicitudes.');
  });

  it('filters by status and goes back to the first page', () => {
    create();
    list().flush(page([sampleInquiry()], 25));
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector('select[aria-label="Filtrar por estado"]') as HTMLSelectElement;
    select.value = 'Closed';
    select.dispatchEvent(new Event('change'));

    const request = list();
    expect(request.request.params.get('status')).toBe('Closed');
    expect(request.request.params.get('page')).toBe('1');
    request.flush(page([]));
  });

  it('searches after a pause, not on every key', () => {
    create();
    list().flush(page([]));
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input[type="search"]') as HTMLInputElement;
    input.value = '  ana ';
    input.dispatchEvent(new Event('input'));
    http.expectNone((r) => r.url === URL);

    vi.advanceTimersByTime(300);

    const request = list();
    expect(request.request.params.get('search')).toBe('ana');
    request.flush(page([]));
  });

  it('pages through the inquiries', () => {
    create();
    list().flush(page([sampleInquiry()], 13));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[aria-label="Página siguiente"]') as HTMLButtonElement).click();

    const request = list();
    expect(request.request.params.get('page')).toBe('2');
    request.flush(page([sampleInquiry({ id: 'q-13', name: 'Maria' })], 13, 2));
    fixture.detectChanges();
    expect(text()).toContain('Maria');
  });

  it('opens the detail on click and reflects an update in the row', () => {
    create();
    list().flush(page([sampleInquiry()]));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-inquiry-detail-modal')).toBeNull();

    (fixture.nativeElement.querySelector('tbody tr') as HTMLElement).click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-inquiry-detail-modal')).not.toBeNull();

    const select = fixture.nativeElement.querySelector('#inquiry-status') as HTMLSelectElement;
    select.value = 'Closed';
    select.dispatchEvent(new Event('change'));
    http.expectOne(`${URL}/q-1/status`).flush(sampleInquiry({ status: 'Closed' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('tbody tr').textContent).toContain('Cerrada');
  });

  it('shows the server error', () => {
    create();
    list().flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-error-alert')).not.toBeNull();
  });
});