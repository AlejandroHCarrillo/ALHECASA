import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, catchError, map, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ErrorAlert } from '../../shared/components/error-alert/error-alert';
import { Logo } from '../../shared/components/logo/logo';
import { Paginator } from '../../shared/components/paginator/paginator';
import { PagedResult } from '../../shared/models/paged-result';
import { resolveApiError } from '../../shared/utils/api-error';
import { AgencyProfile } from '../agency-profile/agency-profile.models';
import { AgencyProfileService } from '../agency-profile/agency-profile.service';
import { AgencyHeader } from './agency-header/agency-header';
import { PublicPropertyCard } from '../landing/public-property-card/public-property-card';
import { PublicProperty, PublicPropertiesService } from '../landing/public-properties.service';

const PAGE_SIZE = 12;

/**
 * Todas las publicaciones activas de una inmobiliaria, tal como las ve un visitante. Es pública (no pide sesión). Se llega desde
 * el «Publica: …» de las tarjetas y del detalle. Una inmobiliaria sin publicaciones activas, o un enlace que no corresponde a
 * ninguna, muestra un aviso en lugar de un error.
 */
@Component({
  selector: 'app-agency-listings',
  imports: [RouterLink, AgencyHeader, ErrorAlert, Logo, Paginator, PublicPropertyCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agency-listings.html',
})
export class AgencyListings {
  private readonly route = inject(ActivatedRoute);
  private readonly publicProperties = inject(PublicPropertiesService);
  private readonly profiles = inject(AgencyProfileService);
  private readonly title = inject(Title);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly result = signal<PagedResult<PublicProperty> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  /** Lo que la inmobiliaria configuró para mostrarse. Nulo si no se pudo cargar o no hay página pública: las publicaciones se ven igual. */
  protected readonly agency = signal<AgencyProfile | null>(null);

  /** El nombre configurado; si la información no cargó, el de las propias publicaciones. */
  protected readonly agencyName = computed(() => this.agency()?.name ?? this.result()?.items[0]?.agencyName ?? null);

  private agencyId = '';
  private page = 1;
  private readonly reload$ = new Subject<void>();

  constructor() {
    // switchMap descarta la respuesta vieja si se cambia de página o de inmobiliaria.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.publicProperties.getByAgency(this.agencyId, this.page, PAGE_SIZE).pipe(
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
          this.updateTitle();
        }
      });

    // La información de la inmobiliaria se pide aparte: si falla (o no existe), las publicaciones se muestran igual.
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        tap(() => this.agency.set(null)),
        switchMap((id) => this.profiles.getPublic(id).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((agency) => {
        this.agency.set(agency);
        this.updateTitle();
      });

    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((id) => {
        this.agencyId = id;
        this.page = 1;
        this.result.set(null);
        this.reload$.next();
      });
  }

  private updateTitle(): void {
    const name = this.agencyName();
    this.title.setTitle(name ? `Publicaciones de ${name} · ALHECASA` : 'Publicaciones de la inmobiliaria · ALHECASA');
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.reload$.next();
    document.getElementById('publicaciones')?.scrollIntoView?.({ behavior: 'smooth' });
  }
}