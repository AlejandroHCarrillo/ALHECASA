import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';
import { LikesService } from '../../core/likes/likes.service';
import { USER_ROLE_LABELS, UserRole } from '../../core/models/user-role';
import { ErrorAlert } from '../../shared/components/error-alert/error-alert';
import { Logo } from '../../shared/components/logo/logo';
import { AdvancedFilters } from '../../shared/components/advanced-filters/advanced-filters';
import { Paginator } from '../../shared/components/paginator/paginator';
import { PagedResult } from '../../shared/models/paged-result';
import { resolveApiError } from '../../shared/utils/api-error';
import {
  NO_ADVANCED_CRITERIA,
  URL_KEYS,
  advancedFromUrl,
  advancedToUrl,
  countAdvanced,
  createAdvancedCriteriaForm,
  normalizeAdvanced,
} from '../properties/property-criteria';
import { OPERATION_LABELS, OperationType, PROPERTY_LIMITS, PROPERTY_TYPE_LABELS, PropertySort, PropertyType } from '../properties/property.models';
import { PublicPropertyCard } from './public-property-card/public-property-card';
import { NO_FILTERS, PublicPropertiesService, PublicProperty, PublicPropertyFilters } from './public-properties.service';

const PAGE_SIZE = 12;

const SORT_DESCRIPTIONS: Record<PropertySort, string> = {
  Recent: 'Las más recientes primero.',
  PriceAsc: 'Del precio más bajo al más alto.',
  PriceDesc: 'Del precio más alto al más bajo.',
  AreaDesc: 'De mayor a menor superficie.',
};

/** Texto estable de un estado de dirección, para compararlo con otro sin importar el orden de los parámetros. */
function serialize(state: Record<string, string>): string {
  return JSON.stringify(Object.entries(state).sort(([a], [b]) => a.localeCompare(b)));
}

interface Step {
  icon: string;
  lead: string;
  text: string;
}

/** Portada pública: solo para quien no ha iniciado sesión. Muestra las propiedades publicadas y lleva al login. */
@Component({
  selector: 'app-landing',
  imports: [ReactiveFormsModule, RouterLink, AdvancedFilters, ErrorAlert, Logo, Paginator, PublicPropertyCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing.html',
})
export class Landing {
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly publicProperties = inject(PublicPropertiesService);
  private readonly likes = inject(LikesService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly locationMaxLength = PROPERTY_LIMITS.city;
  protected readonly operations = Object.values(OperationType);
  protected readonly types = Object.values(PropertyType);
  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly typeLabels = PROPERTY_TYPE_LABELS;

  protected readonly filterForm = this.fb.group({
    operation: ['' as PublicPropertyFilters['operation']],
    location: [''],
    type: ['' as PublicPropertyFilters['type']],
    favoritesOnly: [false],
    advanced: createAdvancedCriteriaForm(),
  });

  /** El panel «Más filtros» está abierto. Se abre solo si el enlace trae filtros avanzados. */
  protected readonly advancedOpen = signal(false);

  /** Cantidad de publicaciones con «me gusta», para el botón «Mis favoritas». */
  protected readonly likesCount = this.likes.count;

  /** Filtros que se están aplicando a la lista (los del formulario solo cuentan al pulsar «Buscar»). */
  protected readonly applied = signal<PublicPropertyFilters>(NO_FILTERS);
  protected readonly hasFilters = computed(() => {
    const f = this.applied();
    return f.operation !== '' || f.type !== '' || f.location.trim() !== '' || f.favoritesOnly || countAdvanced(f) > 0;
  });

  /** Filtros avanzados aplicados (el orden no cuenta), para el contador del botón «Más filtros». */
  protected readonly advancedCount = computed(() => countAdvanced(this.applied()));

  /** Hay algo que quitar: algún filtro o un orden distinto del normal. */
  protected readonly canClear = computed(() => this.hasFilters() || this.applied().sort !== PropertySort.Recent);

  protected readonly sortDescription = computed(() => SORT_DESCRIPTIONS[this.applied().sort]);

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
  /** La dirección (parámetros) que corresponde al estado actual; sirve para no reaccionar a los cambios que la propia pantalla hizo. */
  private currentUrl = '';
  private initialized = false;

  constructor() {
    // switchMap descarta respuestas viejas si el visitante vuelve a buscar o cambia de página.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() => {
          const filters = this.applied();
          // Los «me gusta» viven en el navegador: se piden a la API por identificador.
          const ids = filters.favoritesOnly ? this.likes.ids() : null;

          // Sin favoritas no se consulta: una lista vacía de ids significaría «sin filtro» y devolvería todo.
          if (ids !== null && ids.length === 0) {
            return of<PagedResult<PublicProperty>>({ items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0, totalPages: 0 });
          }

          return this.publicProperties.getPaged(filters, this.page, PAGE_SIZE, ids).pipe(
            catchError((err: unknown) => {
              this.error.set(resolveApiError(err));
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) {
          this.result.set(result);
        }
      });

    // La dirección manda: al entrar (con o sin filtros en el enlace) y cada vez que cambie desde afuera, por ejemplo
    // al pulsar el logo o al abrir un enlace compartido estando en la portada. Lo que la propia pantalla escribe en la
    // dirección se reconoce por `currentUrl` y no vuelve a cargar nada. La primera emisión es síncrona: carga al entrar.
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => this.applyUrl(params));
  }

  protected search(): void {
    const { advanced, ...basic } = this.filterForm.getRawValue();
    this.applied.set({ ...basic, ...normalizeAdvanced(advanced) });
    this.page = 1;
    this.syncUrl();
    this.reload$.next();
    this.scrollToResults();
  }

  protected clearFilters(): void {
    this.filterForm.reset({ operation: '', location: '', type: '', favoritesOnly: false });
    this.filterForm.controls.advanced.reset(NO_ADVANCED_CRITERIA);
    this.search();
  }

  protected toggleAdvanced(): void {
    this.advancedOpen.update((open) => !open);
  }

  /** «Mis favoritas» se aplica al momento, junto con lo que ya esté elegido en el formulario. */
  protected toggleFavorites(): void {
    const control = this.filterForm.controls.favoritesOnly;
    control.setValue(!control.value);
    this.search();
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.syncUrl();
    this.reload$.next();
    this.scrollToResults();
  }

  /** Los filtros y la página como parámetros de la dirección: solo los que tienen valor, para que el enlace sea corto. */
  private urlState(): Record<string, string> {
    const f = this.applied();
    const state: Record<string, string> = {};
    if (f.operation) {
      state[URL_KEYS.operation] = f.operation;
    }
    if (f.type) {
      state[URL_KEYS.type] = f.type;
    }
    if (f.location.trim()) {
      state[URL_KEYS.location] = f.location.trim();
    }
    if (f.favoritesOnly) {
      state[URL_KEYS.favoritesOnly] = '1';
    }
    Object.assign(state, advancedToUrl(f));
    if (this.page > 1) {
      state[URL_KEYS.page] = String(this.page);
    }
    return state;
  }

  /** Escribe el estado actual en la dirección, sin agregar una entrada al historial (cada búsqueda reemplaza a la anterior). */
  private syncUrl(): void {
    const state = this.urlState();
    this.currentUrl = serialize(state);
    this.router.navigate([], { relativeTo: this.route, queryParams: state, replaceUrl: true }).catch(() => undefined);
  }

  /** Aplica los filtros que trae la dirección. Una dirección escrita a mano puede traer cualquier cosa: lo que no se reconoce se ignora. */
  private applyUrl(params: ParamMap): void {
    const incoming = serialize(Object.fromEntries(params.keys.map((key) => [key, params.get(key) ?? ''])));
    if (this.initialized && incoming === this.currentUrl) {
      return; // es la dirección que escribió esta misma pantalla
    }

    const advanced = advancedFromUrl((key) => params.get(key));
    const operation = Object.values(OperationType).find((o) => o === params.get(URL_KEYS.operation)) ?? '';
    const type = Object.values(PropertyType).find((t) => t === params.get(URL_KEYS.type)) ?? '';
    const location = (params.get(URL_KEYS.location) ?? '').trim().slice(0, this.locationMaxLength);
    const favoritesOnly = params.get(URL_KEYS.favoritesOnly) === '1';
    const page = Number(params.get(URL_KEYS.page));

    this.filterForm.reset({ operation, location, type, favoritesOnly });
    this.filterForm.controls.advanced.reset(advanced);
    this.applied.set({ operation, location, type, favoritesOnly, ...advanced });
    this.page = Number.isInteger(page) && page > 1 ? page : 1;
    this.advancedOpen.set(countAdvanced(advanced) > 0);
    this.currentUrl = serialize(this.urlState());
    this.initialized = true;
    this.reload$.next();
  }

  private scrollToResults(): void {
    document.getElementById('propiedades')?.scrollIntoView?.({ behavior: 'smooth' });
  }
}
