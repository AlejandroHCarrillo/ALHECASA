import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';
import { ErrorAlert } from '../../shared/components/error-alert/error-alert';
import { Paginator } from '../../shared/components/paginator/paginator';
import { PagedResult } from '../../shared/models/paged-result';
import { resolveApiError } from '../../shared/utils/api-error';
import { PublicProperty } from '../landing/public-properties.service';
import { PublicPropertyCard } from '../landing/public-property-card/public-property-card';
import { PropertiesService } from '../properties/properties.service';

const PAGE_SIZE = 12;

/**
 * Galería de la inmobiliaria: sus propiedades publicadas con las mismas tarjetas de la portada pública,
 * para que el personal vea cómo las ve un visitante. Solo lectura: para editar se usa «Propiedades».
 */
@Component({
  selector: 'app-gallery',
  imports: [RouterLink, ErrorAlert, Paginator, PublicPropertyCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './gallery.html',
})
export class Gallery {
  private readonly propertiesService = inject(PropertiesService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly result = signal<PagedResult<PublicProperty> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  private page = 1;
  private readonly reload$ = new Subject<void>();

  constructor() {
    // switchMap descarta la respuesta vieja si se cambia de página antes de que llegue.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap(() =>
          this.propertiesService.getGallery(this.page, PAGE_SIZE).pipe(
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

  protected goToPage(page: number): void {
    this.page = page;
    this.reload$.next();
  }
}