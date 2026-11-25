import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Paginator } from '../../../shared/components/paginator/paginator';
import { PagedResult } from '../../../shared/models/paged-result';
import { resolveApiError } from '../../../shared/utils/api-error';
import { InquiriesService } from '../inquiries.service';
import { InquiryDetailModal } from '../inquiry-detail-modal/inquiry-detail-modal';
import { INQUIRY_STATUS_BADGES, INQUIRY_STATUS_LABELS, Inquiry, InquiryStatus } from '../inquiry.models';

const PAGE_SIZE = 10;

/** Bandeja de solicitudes de informes de la inmobiliaria, de la más reciente a la más antigua. */
@Component({
  selector: 'app-inquiry-list',
  imports: [ReactiveFormsModule, DatePipe, ErrorAlert, Paginator, InquiryDetailModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inquiry-list.html',
})
export class InquiryList {
  private readonly inquiriesService = inject(InquiriesService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly statusLabels = INQUIRY_STATUS_LABELS;
  protected readonly statusBadges = INQUIRY_STATUS_BADGES;
  protected readonly statuses: InquiryStatus[] = [InquiryStatus.New, InquiryStatus.InProgress, InquiryStatus.Closed];

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly statusControl = new FormControl<InquiryStatus | ''>('', { nonNullable: true });
  protected readonly result = signal<PagedResult<Inquiry> | null>(null);
  protected readonly loading = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly selected = signal<Inquiry | null>(null);

  private page = 1;
  private readonly reload$ = new Subject<void>();

  constructor() {
    // switchMap descarta respuestas viejas si el usuario sigue escribiendo o cambiando de filtro o página.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.listError.set(null);
        }),
        switchMap(() =>
          this.inquiriesService.getPaged(this.statusControl.value, this.searchControl.value, this.page, PAGE_SIZE).pipe(
            catchError((err: unknown) => {
              this.listError.set(resolveApiError(err));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) {
          this.result.set(result);
        }
      });

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.restart());
    this.statusControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.restart());

    this.reload$.next();
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.reload$.next();
  }

  protected open(inquiry: Inquiry): void {
    this.selected.set(inquiry);
  }

  /** La solicitud cambió dentro del modal: se refleja en la fila sin recargar toda la lista. */
  protected onUpdated(inquiry: Inquiry): void {
    this.selected.set(inquiry);
    this.result.update((current) =>
      current ? { ...current, items: current.items.map((i) => (i.id === inquiry.id ? inquiry : i)) } : current,
    );
  }

  /** Al cerrar se recarga, por si el cambio de estado hace que la solicitud ya no cumpla el filtro. */
  protected close(): void {
    this.selected.set(null);
    if (this.statusControl.value) {
      this.reload$.next();
    }
  }

  private restart(): void {
    this.page = 1;
    this.reload$.next();
  }
}