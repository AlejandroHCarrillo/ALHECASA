import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LikeButton } from '../../../shared/components/like-button/like-button';
import { imageUrl } from '../../../shared/utils/image-url';
import { PROPERTY_TYPE_LABELS } from '../../properties/property.models';
import { PublicProperty } from '../public-properties.service';

/**
 * Tarjeta de una publicación tal como la ve un visitante: foto de portada, corazón de «me gusta», datos clave y enlace al detalle.
 * La usan la portada pública y la galería de la inmobiliaria, para que esta última muestre exactamente lo que se ve afuera.
 */
@Component({
  selector: 'app-public-property-card',
  imports: [RouterLink, DecimalPipe, LikeButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'card relative h-full overflow-hidden rounded-box border border-base-300 bg-base-100' },
  templateUrl: './public-property-card.html',
})
export class PublicPropertyCard {
  readonly data = input.required<PublicProperty>();

  protected readonly typeLabels = PROPERTY_TYPE_LABELS;
  protected readonly imageUrl = imageUrl;

  protected location(property: PublicProperty): string {
    return [property.neighborhood, property.city, property.state].filter(Boolean).join(', ');
  }
}