import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';
import { USER_ROLE_LABELS, UserRole } from '../../core/models/user-role';
import { ErrorAlert } from '../../shared/components/error-alert/error-alert';
import { Logo } from '../../shared/components/logo/logo';
import { Paginator } from '../../shared/components/paginator/paginator';
import { PagedResult } from '../../shared/models/paged-result';
import { resolveApiError } from '../../shared/utils/api-error';
import { imageUrl } from '../../shared/utils/image-url';
import { OPERATION_LABELS, OperationType, PROPERTY_LIMITS, PROPERTY_TYPE_LABELS, PropertyType } from '../properties/property.models';
import { NO_FILTERS, PublicPropertiesService, PublicProperty, PublicPropertyFilters } from './public-properties.service';

const PAGE_SIZE = 12;

interface Step {
  icon: string;
  lead: string;
  text: string;
}

/** Portada pública: solo para quien no ha iniciado sesión. Muestra las propiedades publicadas y lleva al login. */
@Component({
  selector: 'app-landing',
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe, ErrorAlert, Logo, Paginator],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing.html',
})
export class Landing {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly publicProperties = inject(PublicPropertiesService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly locationMaxLength = PROPERTY_LIMITS.city;
  protected readonly operations = Object.values(OperationType);
  protected readonly types = Object.values(PropertyType);
  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly typeLabels = PROPERTY_TYPE_LABELS;
  protected readonly imageUrl = imageUrl;

  protected readonly filterForm = this.fb.group({
    operation: ['' as PublicPropertyFilters['operation']],
    location: [''],
    type: ['' as PublicPropertyFilters['type']],
  });

  /** Filtros que se están aplicando a la lista (los del formulario solo cuentan al pulsar «Buscar»). */
  protected readonly applied = signal<PublicPropertyFilters>(NO_FILTERS);
  protected readonly hasFilters = computed(() => {
    const f = this.applied();
    return f.operation !== '' || f.type !== '' || f.location.trim() !== '';
  });

  protected readonly result = signal<PagedResult<PublicProperty> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly steps: Step[] = [
    { icon: 'fa-pen-to-square', lead: 'Captura', text: 'La inmobiliaria da de alta cada propiedad con su precio, ubicación y características.' },
    { icon: 'fa-bullhorn', lead: 'Publica', text: 'Al pasar una propiedad a «Publicada» aparece en esta página para cualquier visitante.' },
    { icon: 'fa-users-gear', lead: 'Coordina', text: 'El administrador da de alta a su equipo de supervisores y vendedores, que mantienen el inventario.' },
  ];

  protected readonly roles: { role: UserRole; text: string }[] = [
    { role: UserRole.PlatformAdmin, text: 'Ve y administra todas las inmobiliarias, sus usuarios y sus propiedades.' },
    { role: UserRole.AgencyAdmin, text: 'Administra los usuarios y las propiedades de su inmobiliaria.' },
    { role: UserRole.Supervisor, text: 'Gestiona las propiedades de su inmobiliaria.' },
    { role: UserRole.Seller, text: 'Gestiona las propiedades de su inmobiliaria.' },
    { role: UserRole.Client, text: 'Tiene cuenta, pero todavía no cuenta con herramientas de administración.' },
  ];

  protected readonly roleLabels = USER_ROLE_LABELS;

  private page = 1;
  private readonly reload$ = new Subject<void>();

  constructor() {
    // switchMap descarta respuestas viejas si el visitante vuelve a buscar o cambia de página.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.publicProperties.getPaged(this.applied(), this.page, PAGE_SIZE).pipe(
            catchError((err: unknown) => {
              this.error.set(resolveApiError(err));
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

    this.reload$.next();
  }

  protected search(): void {
    this.applied.set(this.filterForm.getRawValue());
    this.page = 1;
    this.reload$.next();
    this.scrollToResults();
  }

  protected clearFilters(): void {
    this.filterForm.reset({ operation: '', location: '', type: '' });
    this.search();
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.reload$.next();
    this.scrollToResults();
  }

  protected location(property: PublicProperty): string {
    return [property.neighborhood, property.city, property.state].filter(Boolean).join(', ');
  }

  private scrollToResults(): void {
    document.getElementById('propiedades')?.scrollIntoView?.({ behavior: 'smooth' });
  }
}
