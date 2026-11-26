import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { AdvancedCriteriaForm } from '../../../features/properties/property-criteria';
import { AMENITY_LABELS, Amenity, CurrencyCode, PROPERTY_LIMITS, PropertySort, SORT_LABELS } from '../../../features/properties/property.models';

/**
 * Campos de los filtros avanzados (precio, recámaras, baños, estacionamientos, superficie, mascotas, amueblado,
 * amenidades y orden). Solo dibuja los campos de un formulario que le da la pantalla: no tiene botón de buscar, porque
 * vive dentro del formulario de la pantalla, que decide cuándo aplicar. Lo usan la portada pública y la lista del personal.
 */
@Component({
  selector: 'app-advanced-filters',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [formGroup]="group()" class="space-y-4" data-testid="advanced-filters">
      <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <fieldset class="col-span-2 space-y-1">
          <legend class="text-xs font-semibold uppercase text-base-content/60">Precio</legend>
          <div class="grid grid-cols-[5rem_1fr_1fr] gap-2">
            <div>
              <label class="sr-only" [for]="id('currency')">Moneda</label>
              <select [id]="id('currency')" class="select select-bordered select-sm w-full" formControlName="currency">
                @for (currency of currencies; track currency) {
                  <option [value]="currency">{{ currency }}</option>
                }
              </select>
            </div>
            <div>
              <label class="sr-only" [for]="id('minPrice')">Precio mínimo</label>
              <input [id]="id('minPrice')" type="number" min="0" [max]="limits.maxPrice" step="any" inputmode="decimal" placeholder="Mínimo" class="input input-bordered input-sm w-full" formControlName="minPrice" />
            </div>
            <div>
              <label class="sr-only" [for]="id('maxPrice')">Precio máximo</label>
              <input [id]="id('maxPrice')" type="number" min="0" [max]="limits.maxPrice" step="any" inputmode="decimal" placeholder="Máximo" class="input input-bordered input-sm w-full" formControlName="maxPrice" />
            </div>
          </div>
        </fieldset>

        <fieldset class="col-span-2 space-y-1">
          <legend class="text-xs font-semibold uppercase text-base-content/60">Superficie (m²)</legend>
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="sr-only" [for]="id('minArea')">Superficie mínima</label>
              <input [id]="id('minArea')" type="number" min="0" [max]="limits.maxArea" step="any" inputmode="decimal" placeholder="Mínima" class="input input-bordered input-sm w-full" formControlName="minArea" />
            </div>
            <div>
              <label class="sr-only" [for]="id('maxArea')">Superficie máxima</label>
              <input [id]="id('maxArea')" type="number" min="0" [max]="limits.maxArea" step="any" inputmode="decimal" placeholder="Máxima" class="input input-bordered input-sm w-full" formControlName="maxArea" />
            </div>
          </div>
        </fieldset>

        <div class="space-y-1">
          <label class="text-xs font-semibold uppercase text-base-content/60" [for]="id('minBedrooms')">Recámaras (mín.)</label>
          <input [id]="id('minBedrooms')" type="number" min="0" [max]="limits.maxRooms" step="1" placeholder="Cualquiera" class="input input-bordered input-sm w-full" formControlName="minBedrooms" />
        </div>

        <div class="space-y-1">
          <label class="text-xs font-semibold uppercase text-base-content/60" [for]="id('minBathrooms')">Baños (mín.)</label>
          <input [id]="id('minBathrooms')" type="number" min="0" [max]="limits.maxRooms" step="1" placeholder="Cualquiera" class="input input-bordered input-sm w-full" formControlName="minBathrooms" />
        </div>

        <div class="space-y-1">
          <label class="text-xs font-semibold uppercase text-base-content/60" [for]="id('minParking')">Estacionamientos (mín.)</label>
          <input [id]="id('minParking')" type="number" min="0" [max]="limits.maxParking" step="1" placeholder="Cualquiera" class="input input-bordered input-sm w-full" formControlName="minParking" />
        </div>

        <div class="space-y-1">
          <label class="text-xs font-semibold uppercase text-base-content/60" [for]="id('sort')">Ordenar por</label>
          <select [id]="id('sort')" class="select select-bordered select-sm w-full" formControlName="sort">
            @for (sort of sorts; track sort) {
              <option [value]="sort">{{ sortLabels[sort] }}</option>
            }
          </select>
        </div>
      </div>

      <div class="flex flex-wrap gap-x-6 gap-y-1">
        <label class="label cursor-pointer justify-start gap-2 py-0" [for]="id('petsAllowed')">
          <input [id]="id('petsAllowed')" type="checkbox" class="checkbox checkbox-sm" formControlName="petsAllowed" />
          <span class="label-text">Acepta mascotas</span>
        </label>
        <label class="label cursor-pointer justify-start gap-2 py-0" [for]="id('furnished')">
          <input [id]="id('furnished')" type="checkbox" class="checkbox checkbox-sm" formControlName="furnished" />
          <span class="label-text">Amueblada</span>
        </label>
      </div>

      <fieldset class="space-y-1">
        <legend class="text-xs font-semibold uppercase text-base-content/60">Amenidades (con todas las que elijas)</legend>
        <div class="grid grid-cols-2 gap-x-4 gap-y-1 sm:grid-cols-3 lg:grid-cols-4" data-testid="amenity-filters">
          @for (amenity of amenities; track amenity) {
            <label class="label cursor-pointer justify-start gap-2 py-0" [for]="id('amenity-' + amenity)">
              <input
                [id]="id('amenity-' + amenity)"
                type="checkbox"
                class="checkbox checkbox-sm"
                [checked]="hasAmenity(amenity)"
                (change)="toggleAmenity(amenity)"
              />
              <span class="label-text">{{ amenityLabels[amenity] }}</span>
            </label>
          }
        </div>
      </fieldset>
    </div>
  `,
})
export class AdvancedFilters {
  /** Formulario de los criterios (ver `createAdvancedCriteriaForm`). Lo crea y lo aplica la pantalla. */
  readonly group = input.required<AdvancedCriteriaForm>();

  /** Prefijo de los id de los campos, para que dos paneles en la misma página no repitan ids. */
  readonly idPrefix = input('adv');

  protected readonly limits = { ...PROPERTY_LIMITS };
  protected readonly currencies = Object.values(CurrencyCode);
  protected readonly sorts = Object.values(PropertySort);
  protected readonly sortLabels = SORT_LABELS;
  protected readonly amenities = Object.values(Amenity);
  protected readonly amenityLabels = AMENITY_LABELS;

  protected id(name: string): string {
    return `${this.idPrefix()}-${name}`;
  }

  protected hasAmenity(amenity: Amenity): boolean {
    return this.group().controls.amenities.value.includes(amenity);
  }

  protected toggleAmenity(amenity: Amenity): void {
    const control = this.group().controls.amenities;
    const current = control.value;
    control.setValue(current.includes(amenity) ? current.filter((a) => a !== amenity) : [...current, amenity]);
  }
}
