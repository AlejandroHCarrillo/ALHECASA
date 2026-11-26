import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, map, of, switchMap, tap } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';
import { ErrorAlert } from '../../../shared/components/error-alert/error-alert';
import { Logo } from '../../../shared/components/logo/logo';
import { Paginator } from '../../../shared/components/paginator/paginator';
import { PagedResult } from '../../../shared/models/paged-result';
import { SeoService } from '../../../shared/services/seo.service';
import { resolveApiError } from '../../../shared/utils/api-error';
import { imageUrl } from '../../../shared/utils/image-url';
import { PublicPropertyCard } from '../../landing/public-property-card/public-property-card';
import { PublicProperty, PublicPropertiesService } from '../../landing/public-properties.service';
import { OperationType } from '../../properties/property.models';
import { ZONE_OPERATIONS, Zone, ZoneLink, ZoneOperation, mainOperation, operationCount, zoneCommands, zonePath } from '../zone.models';
import { ZonesService } from '../zones.service';

const PAGE_SIZE = 12;

const PHRASE: Record<ZoneOperation, string> = { venta: 'en venta', renta: 'en renta' };
const OPERATION: Record<ZoneOperation, OperationType> = { venta: OperationType.Sale, renta: OperationType.Rent };

/**
 * Página pública de una zona: las propiedades en venta o en renta de una ciudad, o de una colonia de ella
 * (`/venta/guadalajara`, `/renta/guadalajara/providencia`). Está pensada para los buscadores: dirección limpia,
 * título, descripción y dirección canónica propios, ruta de migas con datos estructurados y enlaces a las colonias vecinas.
 * Una zona que no existe, o que no tiene nada en esa operación, pide a los buscadores no indexarla.
 */
@Component({
  selector: 'app-zone-listings',
  imports: [RouterLink, ErrorAlert, Logo, Paginator, PublicPropertyCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './zone-listings.html',
})
export class ZoneListings {
  private readonly route = inject(ActivatedRoute);
  private readonly zones = inject(ZonesService);
  private readonly publicProperties = inject(PublicPropertiesService);
  private readonly seo = inject(SeoService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly operations = ZONE_OPERATIONS;
  protected readonly zone = signal<Zone | null>(null);
  protected readonly result = signal<PagedResult<PublicProperty> | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly operation = signal<ZoneOperation>('venta');

  protected readonly zoneName = computed(() => {
    const zone = this.zone();
    return zone?.neighborhoodName ? `${zone.neighborhoodName}, ${zone.cityName}` : (zone?.cityName ?? '');
  });
  protected readonly place = computed(() => {
    const zone = this.zone();
    return zone ? placeName(zone) : '';
  });
  protected readonly phrase = computed(() => PHRASE[this.operation()]);
  protected readonly count = computed(() => {
    const zone = this.zone();
    return zone ? operationCount(zone, this.operation()) : 0;
  });
  protected readonly otherOperation = computed<ZoneOperation>(() => (this.operation() === 'venta' ? 'renta' : 'venta'));
  protected readonly otherCount = computed(() => {
    const zone = this.zone();
    return zone ? operationCount(zone, this.otherOperation()) : 0;
  });

  /** Colonias para enlazar: las de la ciudad, sin la que se está viendo. */
  protected readonly neighbors = computed(() => {
    const zone = this.zone();
    return (zone?.neighborhoods ?? []).filter((n) => n.neighborhoodSlug !== zone?.neighborhoodSlug);
  });

  private citySlug = '';
  private neighborhoodSlug: string | null = null;
  private page = 1;
  private readonly reloadPage$ = new Subject<void>();

  constructor() {
    // La zona sale de la dirección. switchMap descarta lo pendiente si se pasa a otra zona o a otra operación.
    combineLatest([this.route.paramMap, this.route.data])
      .pipe(
        map(([params, data]) => ({
          citySlug: params.get('city') ?? '',
          neighborhoodSlug: params.get('neighborhood'),
          operation: (data['operation'] === 'renta' ? 'renta' : 'venta') as ZoneOperation,
        })),
        tap((target) => {
          this.citySlug = target.citySlug;
          this.neighborhoodSlug = target.neighborhoodSlug;
          this.operation.set(target.operation);
          this.page = 1;
          this.zone.set(null);
          this.result.set(null);
          this.notFound.set(false);
          this.error.set(null);
          this.loading.set(true);
        }),
        switchMap((target) =>
          this.zones.get(target.citySlug, target.neighborhoodSlug ?? undefined).pipe(
            catchError((err: unknown) => {
              if (err instanceof HttpErrorResponse && err.status === 404) {
                this.notFound.set(true);
              } else {
                this.error.set(resolveApiError(err));
              }
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((zone) => {
        this.zone.set(zone);
        if (zone) {
          this.reloadPage$.next();
        } else {
          this.loading.set(false);
          this.updateSeo();
        }
      });

    this.reloadPage$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.publicProperties
            .getByZone(OPERATION[this.operation()], this.citySlug, this.neighborhoodSlug, this.page, PAGE_SIZE)
            .pipe(
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
        this.updateSeo();
      });
  }

  /** Enlace a otra colonia de la ciudad, en la operación que se está viendo si tiene algo en ella (y, si no, en la que sí tiene). */
  protected neighborCommands(link: ZoneLink): string[] {
    const current = this.operation();
    const operation = operationCount(link, current) > 0 ? current : mainOperation(link);
    return zoneCommands(operation, link.citySlug, link.neighborhoodSlug);
  }

  /** Enlace a la ciudad de la zona (para la ruta de navegación de una colonia), en la operación que se está viendo. */
  protected cityCommands(): string[] {
    const zone = this.zone();
    return zone ? zoneCommands(this.operation(), zone.citySlug) : ['/'];
  }

  /** Enlace a la misma zona en la otra operación (de venta a renta y al revés). */
  protected switchCommands(operation: ZoneOperation): string[] {
    const zone = this.zone();
    return zone ? zoneCommands(operation, zone.citySlug, zone.neighborhoodSlug) : ['/'];
  }

  protected goToPage(page: number): void {
    this.page = page;
    this.reloadPage$.next();
    document.getElementById('publicaciones')?.scrollIntoView?.({ behavior: 'smooth' });
  }

  private updateSeo(): void {
    const zone = this.zone();
    const operation = this.operation();

    if (!zone) {
      this.seo.set({
        title: 'Zona no encontrada · ALHECASA',
        description: 'No encontramos propiedades en esta zona. Explora las propiedades en venta y en renta de ALHECASA.',
        noindex: true,
      });
      return;
    }

    const name = this.zoneName();
    const where = placeName(zone);
    const count = operationCount(zone, operation);
    const path = zonePath(operation, zone.citySlug, zone.neighborhoodSlug);
    const noun = count === 1 ? 'propiedad' : 'propiedades';

    this.seo.set({
      title: `Propiedades ${PHRASE[operation]} en ${name} · ALHECASA`,
      description:
        count > 0
          ? `${count} ${noun} ${PHRASE[operation]} en ${where}: casas, departamentos, terrenos y más, con fotos y precios. Contacta directo a la inmobiliaria.`
          : `Por ahora no hay propiedades ${PHRASE[operation]} en ${where}. Mira otras zonas en ALHECASA.`,
      path,
      // Una zona vacía en esta operación no aporta nada a un buscador: se pide no indexarla (sus enlaces se siguen).
      noindex: count === 0,
      image: this.firstImage(),
      jsonLd: this.breadcrumb(zone, operation),
    });
  }

  /** La foto de la primera publicación, para la vista previa al compartir. */
  private firstImage(): string | undefined {
    const id = this.result()?.items[0]?.imageIds[0];
    return id ? imageUrl(id) : undefined;
  }

  /** Ruta de migas para los buscadores: Inicio › En venta › Ciudad › Colonia. */
  private breadcrumb(zone: Zone, operation: ZoneOperation): object {
    const crumbs = [
      { name: 'Inicio', path: '/' },
      { name: `Propiedades ${PHRASE[operation]} en ${zone.cityName}`, path: zonePath(operation, zone.citySlug) },
    ];
    if (zone.neighborhoodName && zone.neighborhoodSlug) {
      crumbs.push({ name: zone.neighborhoodName, path: zonePath(operation, zone.citySlug, zone.neighborhoodSlug) });
    }

    return {
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: crumbs.map((crumb, index) => ({
        '@type': 'ListItem',
        position: index + 1,
        name: crumb.name,
        item: this.seo.absolute(crumb.path),
      })),
    };
  }
}

/** «Polanco, Ciudad de México» o «Guadalajara, Jalisco»: de lo más específico a lo más general, sin repetir («Ciudad de México, Ciudad de México»). */
function placeName(zone: Zone): string {
  const parts = [zone.neighborhoodName, zone.cityName, zone.stateName].filter((part): part is string => !!part);
  return parts.filter((part, index) => index === 0 || part !== parts[index - 1]).join(', ');
}