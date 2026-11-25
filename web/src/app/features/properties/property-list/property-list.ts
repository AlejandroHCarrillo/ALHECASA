import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, distinctUntilChanged, merge, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { UserRole } from '../../../core/models/user-role';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Modal } from '../../../shared/components/modal/modal';
import { AdvancedFilters } from '../../../shared/components/advanced-filters/advanced-filters';
import { Paginator } from '../../../shared/components/paginator/paginator';
import { PagedResult } from '../../../shared/models/paged-result';
import { ToastService } from '../../../shared/services/toast.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { Agency } from '../../users/user.models';
import { NO_LIST_FILTERS, PropertiesService, PropertyListFilters } from '../properties.service';
import { NO_ADVANCED_CRITERIA, countAdvanced, createAdvancedCriteriaForm, normalizeAdvanced } from '../property-criteria';
import { PropertyDetailModal } from '../property-detail-modal/property-detail-modal';
import { PropertyFormModal } from '../property-form-modal/property-form-modal';
import {
  OPERATION_LABELS,
  OperationType,
  PROPERTY_STATUS_BADGES,
  PROPERTY_STATUS_LABELS,
  PROPERTY_TYPE_LABELS,
  Property,
  PropertySort,
  PropertyStatus,
  PropertyType,
} from '../property.models';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-property-list',
  imports: [ReactiveFormsModule, DecimalPipe, AdvancedFilters, ErrorAlert, Modal, Paginator, PropertyFormModal, PropertyDetailModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './property-list.html',
})
export class PropertyList implements OnInit {
  private readonly propertiesService = inject(PropertiesService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly typeLabels = PROPERTY_TYPE_LABELS;
  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly statusLabels = PROPERTY_STATUS_LABELS;
  protected readonly statusBadges = PROPERTY_STATUS_BADGES;

  protected readonly searchControl = new FormControl('', { nonNullable: true });

  protected readonly statuses = Object.values(PropertyStatus);
  protected readonly types = Object.values(PropertyType);
  protected readonly operations = Object.values(OperationType);

  /** Estado, tipo y operación se aplican al elegirlos; los filtros avanzados, al pulsar «Aplicar filtros». */
  protected readonly filterForm = new FormGroup({
    status: new FormControl<PropertyStatus | ''>('', { nonNullable: true }),
    type: new FormControl<PropertyType | ''>('', { nonNullable: true }),
    operation: new FormControl<OperationType | ''>('', { nonNullable: true }),
    advanced: createAdvancedCriteriaForm(),
  });

  protected readonly advancedOpen = signal(false);
  private readonly applied = signal<PropertyListFilters>(NO_LIST_FILTERS);
  protected readonly advancedCount = computed(() => countAdvanced(this.applied()));
  /** Hay algo que quitar: un filtro, un orden distinto del normal o texto de búsqueda. */
  protected readonly canClear = computed(() => {
    const f = this.applied();
    return f.status !== '' || f.type !== '' || f.operation !== '' || f.sort !== PropertySort.Recent || countAdvanced(f) > 0;
  });
  protected readonly result = signal<PagedResult<Property> | null>(null);
  protected readonly loading = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly agencies = signal<Agency[]>([]);

  // Estado de los modales
  protected readonly showForm = signal(false);
  protected readonly formProperty = signal<Property | null>(null);
  protected readonly showDetail = signal(false);
  protected readonly detailProperty = signal<Property | null>(null);
  protected readonly deleteTarget = signal<Property | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  private page = 1;
  private readonly reload$ = new Subject<void>();

  constructor() {
    // switchMap descarta respuestas viejas si el usuario sigue escribiendo o cambiando de página.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.listError.set(null);
        }),
        switchMap(() =>
          this.propertiesService.getPaged({ ...this.applied(), search: this.searchControl.value }, this.page, PAGE_SIZE).pipe(
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

    // Búsqueda con debounce de 300 ms.
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.page = 1;
        this.reload$.next();
      });

    // Estado, tipo y operación se aplican al elegirlos, con los filtros avanzados que ya estaban aplicados.
    const { status, type, operation } = this.filterForm.controls;
    merge(status.valueChanges, type.valueChanges, operation.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.applied.update((a) => ({ ...a, status: status.value, type: type.value, operation: operation.value }));
        this.page = 1;
        this.reload$.next();
      });
  }

  /** El administrador de plataforma ve todas las inmobiliarias, así que la lista muestra a cuál pertenece cada propiedad. */
  protected get isPlatformAdmin(): boolean {
    return this.auth.hasRole(UserRole.PlatformAdmin);
  }

  ngOnInit(): void {
    this.reload$.next();

    // Solo el administrador de plataforma elige inmobiliaria; la lista alimenta el formulario.
    if (this.isPlatformAdmin) {
      this.propertiesService.getAgencies().subscribe({
        next: (agencies) => this.agencies.set(agencies),
        error: (err: unknown) => this.listError.set(resolveApiError(err)),
      });
    }
  }

  protected toggleAdvanced(): void {
    this.advancedOpen.update((open) => !open);
  }

  protected applyAdvanced(): void {
    this.applied.update((a) => ({ ...a, ...normalizeAdvanced(this.filterForm.controls.advanced.getRawValue()) }));
    this.page = 1;
    this.reload$.next();
  }

  protected clearFilters(): void {
    // Sin emitir eventos: restablecer los selectores dispararía una consulta por cada uno.
    this.filterForm.reset({ status: '', type: '', operation: '' }, { emitEvent: false });
    this.filterForm.controls.advanced.reset(NO_ADVANCED_CRITERIA, { emitEvent: false });
    this.applied.update((a) => ({ ...a, ...NO_LIST_FILTERS, search: a.search }));
    this.page = 1;
    this.reload$.next();
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.reload$.next();
  }

  // --- Detalle (solo lectura) ---
  protected openDetail(property: Property): void {
    this.detailProperty.set(property);
    this.showDetail.set(true);
  }

  protected closeDetail(): void {
    this.showDetail.set(false);
    this.detailProperty.set(null);
  }

  protected editFromDetail(): void {
    const property = this.detailProperty();
    this.closeDetail();
    if (property) {
      this.openForm(property);
    }
  }

  // --- Alta / edición ---
  protected openForm(property: Property | null): void {
    this.formProperty.set(property);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
    this.formProperty.set(null);
  }

  protected onSaved(message: string): void {
    const wasCreate = this.formProperty() === null;
    this.closeForm();
    this.toast.success(message);
    if (wasCreate) {
      this.page = 1;
    }
    this.reload$.next();
  }

  // --- Eliminación (lógica) ---
  protected askDelete(property: Property): void {
    this.deleteError.set(null);
    this.deleteTarget.set(property);
  }

  protected cancelDelete(): void {
    this.deleteTarget.set(null);
    this.deleteError.set(null);
  }

  protected confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    this.propertiesService.delete(target.id).subscribe({
      next: () => {
        this.deleting.set(false);
        this.deleteTarget.set(null);
        this.toast.success('Propiedad eliminada correctamente.');

        // Si se eliminó el último registro de la página, regresa a la anterior.
        if (this.result()?.items.length === 1 && this.page > 1) {
          this.page--;
        }
        this.reload$.next();
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.deleteError.set(resolveApiError(err));
      },
    });
  }
}
