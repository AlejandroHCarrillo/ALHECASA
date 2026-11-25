import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ErrorAlert } from '../../shared/components/error-alert/error-alert';
import { LikeButton } from '../../shared/components/like-button/like-button';
import { LocationMap } from '../../shared/components/location-map/location-map';
import { Logo } from '../../shared/components/logo/logo';
import { resolveApiError } from '../../shared/utils/api-error';
import { imageUrl } from '../../shared/utils/image-url';
import { PublicProperty, PublicPropertiesService } from '../landing/public-properties.service';
import { InquiryFormModal } from './inquiry-form-modal/inquiry-form-modal';
import { AMENITY_LABELS, OPERATION_LABELS, PROPERTY_TYPE_LABELS } from '../properties/property.models';

/**
 * Detalle público de una publicación: todos los datos que se publican y la galería completa.
 * No requiere sesión. Si la propiedad no existe o dejó de estar publicada, muestra un aviso.
 */
@Component({
  selector: 'app-public-property-detail',
  imports: [RouterLink, DecimalPipe, ErrorAlert, InquiryFormModal, LikeButton, LocationMap, Logo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './public-property-detail.html',
})
export class PublicPropertyDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly publicProperties = inject(PublicPropertiesService);
  private readonly title = inject(Title);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly typeLabels = PROPERTY_TYPE_LABELS;
  protected readonly amenityLabels = AMENITY_LABELS;
  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly imageUrl = imageUrl;

  protected readonly property = signal<PublicProperty | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);

  /** El modal para pedir informes está abierto. */
  protected readonly inquiryOpen = signal(false);

  /** Posición de la foto grande dentro de la galería. */
  protected readonly selected = signal(0);
  protected readonly imageIds = computed(() => this.property()?.imageIds ?? []);
  protected readonly currentImage = computed(() => this.imageIds()[this.selected()] ?? null);

  protected readonly location = computed(() => {
    const p = this.property();
    return p ? [p.neighborhood, p.city, p.state].filter(Boolean).join(', ') : '';
  });

  /** Zona aproximada para el mapa; null si la propiedad no tiene ubicación. */
  protected readonly mapPosition = computed(() => {
    const p = this.property();
    return p && p.approximateLatitude !== null && p.approximateLongitude !== null
      ? { latitude: p.approximateLatitude, longitude: p.approximateLongitude }
      : null;
  });

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id') ?? ''),
        tap(() => {
          this.loading.set(true);
          this.notFound.set(false);
          this.error.set(null);
          this.property.set(null);
          this.selected.set(0);
        }),
        // switchMap descarta la respuesta vieja si se navega a otra publicación.
        switchMap((id) =>
          this.publicProperties.getById(id).pipe(
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
      .subscribe((property) => {
        this.loading.set(false);
        this.property.set(property);
        if (property) {
          this.title.setTitle(`${property.title} · ALHECASA`);
        }
      });
  }

  protected select(index: number): void {
    this.selected.set(index);
  }

  /** Avanza o retrocede una foto, dando la vuelta al llegar al extremo. */
  protected step(delta: number): void {
    const total = this.imageIds().length;
    if (total > 1) {
      this.selected.update((current) => (current + delta + total) % total);
    }
  }
}
