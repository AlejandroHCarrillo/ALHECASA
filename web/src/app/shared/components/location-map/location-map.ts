import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, InjectionToken, afterNextRender, effect, inject, input, signal, viewChild } from '@angular/core';
import type * as Leaflet from 'leaflet';

type LeafletModule = typeof Leaflet;

/**
 * Cómo se carga Leaflet. Se importa de forma diferida: solo quien abre un detalle con ubicación descarga la librería,
 * y las pruebas pueden sustituirla sin tocar el módulo real.
 */
export const LEAFLET_LOADER = new InjectionToken<() => Promise<LeafletModule>>('LEAFLET_LOADER', {
  providedIn: 'root',
  factory: () => async () => {
    const module = (await import('leaflet')) as LeafletModule & { default?: LeafletModule };
    return module.default ?? module;
  },
});

/** Radio del círculo que marca la zona. Mayor que la imprecisión de las coordenadas públicas (unos 110 m). */
export const AREA_RADIUS_METERS = 250;
export const MAP_ZOOM = 15;

const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';

/**
 * Mapa con la zona de una propiedad: un círculo, no un marcador, porque la dirección exacta no se publica.
 * Los mosaicos son de OpenStreetMap (gratuitos, con atribución obligatoria); para un tráfico alto hay que usar un proveedor propio.
 * La rueda del ratón no hace zoom para no atrapar el scroll de la página.
 */
@Component({
  selector: 'app-location-map',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative isolate">
      <div
        #host
        class="h-72 w-full overflow-hidden rounded-box bg-base-200 sm:h-96"
        role="region"
        [attr.aria-label]="label()"
        data-testid="map"
      ></div>
      @if (failed()) {
        <p class="absolute inset-0 flex items-center justify-center p-4 text-center text-sm text-base-content/70" role="alert">
          No se pudo cargar el mapa. Revisa tu conexión e inténtalo de nuevo.
        </p>
      }
    </div>
  `,
})
export class LocationMap {
  private readonly loadLeaflet = inject(LEAFLET_LOADER);
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');

  readonly latitude = input.required<number>();
  readonly longitude = input.required<number>();
  readonly label = input('Mapa con la ubicación aproximada de la propiedad');

  protected readonly failed = signal(false);

  private readonly map = signal<Leaflet.Map | null>(null);
  private circle: Leaflet.Circle | null = null;
  private destroyed = false;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.map()?.remove();
    });

    afterNextRender(() => {
      this.loadLeaflet().then(
        (leaflet) => {
          if (this.destroyed) {
            return;
          }
          const map = leaflet.map(this.host().nativeElement, { scrollWheelZoom: false });
          leaflet.tileLayer(TILE_URL, { maxZoom: 19, attribution: ATTRIBUTION }).addTo(map);
          this.circle = leaflet
            .circle([this.latitude(), this.longitude()], { radius: AREA_RADIUS_METERS, color: '#0f766e', fillColor: '#0f766e', fillOpacity: 0.2 })
            .addTo(map);
          this.map.set(map);
        },
        () => this.failed.set(true),
      );
    });

    // Si cambian las coordenadas (se navega a otra publicación sin recrear el componente), se recoloca el mapa.
    effect(() => {
      const map = this.map();
      const latLng: [number, number] = [this.latitude(), this.longitude()];
      if (map) {
        map.setView(latLng, MAP_ZOOM);
        this.circle?.setLatLng(latLng);
      }
    });
  }
}