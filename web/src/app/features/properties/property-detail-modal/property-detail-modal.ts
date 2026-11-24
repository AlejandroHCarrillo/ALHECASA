import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Modal } from '../../../shared/components/modal/modal';
import { imageUrl } from '../../../shared/utils/image-url';
import {
  OPERATION_LABELS,
  PROPERTY_STATUS_BADGES,
  PROPERTY_STATUS_LABELS,
  PROPERTY_TYPE_LABELS,
  Property,
} from '../property.models';

/** Modal de solo lectura con los datos de la propiedad. */
@Component({
  selector: 'app-property-detail-modal',
  imports: [Modal, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal title="Detalle de la propiedad" (closed)="closed.emit()">
      @if (property().imageIds.length > 0) {
        <ul class="mb-4 grid grid-cols-3 gap-2 sm:grid-cols-4" aria-label="Imágenes de la propiedad" data-testid="gallery">
          @for (id of property().imageIds; track id; let first = $first) {
            <li [class]="first ? 'col-span-3 sm:col-span-4' : ''">
              <a [href]="imageUrl(id)" target="_blank" rel="noopener">
                <img
                  [src]="imageUrl(id)"
                  [alt]="'Imagen ' + ($index + 1) + ' de ' + property().title"
                  class="w-full rounded-box bg-base-200 object-cover"
                  [class]="first ? 'aspect-video' : 'aspect-4/3'"
                  loading="lazy"
                />
              </a>
            </li>
          }
        </ul>
      } @else {
        <p class="mb-4 rounded-box bg-base-200 p-3 text-sm text-base-content/70">Esta propiedad todavía no tiene imágenes.</p>
      }

      <dl class="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
        <div class="sm:col-span-2">
          <dt class="text-xs uppercase text-base-content/60">Título</dt>
          <dd class="font-medium">{{ property().title }}</dd>
        </div>
        <div class="sm:col-span-2">
          <dt class="text-xs uppercase text-base-content/60">Descripción</dt>
          <dd class="whitespace-pre-line">{{ property().description ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Tipo</dt>
          <dd>{{ typeLabels[property().type] }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Operación</dt>
          <dd>{{ operationLabels[property().operation] }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Precio</dt>
          <dd>{{ property().currency }} {{ property().price | number: '1.2-2' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Estado</dt>
          <dd>
            <span class="badge" [class]="statusBadges[property().status]">{{ statusLabels[property().status] }}</span>
          </dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Calle y número</dt>
          <dd>{{ property().street ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Colonia</dt>
          <dd>{{ property().neighborhood ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Ciudad</dt>
          <dd>{{ property().city }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Estado (entidad)</dt>
          <dd>{{ property().state }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Código postal</dt>
          <dd>{{ property().postalCode ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Inmobiliaria</dt>
          <dd>{{ property().agencyName ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Recámaras</dt>
          <dd>{{ property().bedrooms ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Baños</dt>
          <dd>{{ property().bathrooms ?? '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Construcción (m²)</dt>
          <dd>{{ property().constructionArea !== null ? (property().constructionArea | number: '1.0-2') : '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Terreno (m²)</dt>
          <dd>{{ property().landArea !== null ? (property().landArea | number: '1.0-2') : '—' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Alta</dt>
          <dd>{{ property().createdAt | date: 'dd/MM/yyyy HH:mm' }}</dd>
        </div>
        <div>
          <dt class="text-xs uppercase text-base-content/60">Última modificación</dt>
          <dd>{{ property().updatedAt ? (property().updatedAt | date: 'dd/MM/yyyy HH:mm') : '—' }}</dd>
        </div>
      </dl>

      <div class="modal-action">
        <button type="button" class="btn" (click)="closed.emit()">Cerrar</button>
        <button type="button" class="btn btn-warning" (click)="edit.emit()">
          <i class="fa-solid fa-pen" aria-hidden="true"></i>
          Editar
        </button>
      </div>
    </app-modal>
  `,
})
export class PropertyDetailModal {
  readonly property = input.required<Property>();
  readonly closed = output<void>();
  readonly edit = output<void>();

  protected readonly imageUrl = imageUrl;
  protected readonly typeLabels = PROPERTY_TYPE_LABELS;
  protected readonly operationLabels = OPERATION_LABELS;
  protected readonly statusLabels = PROPERTY_STATUS_LABELS;
  protected readonly statusBadges = PROPERTY_STATUS_BADGES;
}
