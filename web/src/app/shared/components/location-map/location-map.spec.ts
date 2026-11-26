import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AREA_RADIUS_METERS, LEAFLET_LOADER, LocationMap, MAP_ZOOM } from './location-map';

/** Doble mínimo de Leaflet: registra lo que el componente le pide, sin dibujar nada. */
function createFakeLeaflet() {
  const calls = {
    mapOptions: null as unknown,
    tileUrl: '',
    tileOptions: null as { attribution?: string } | null,
    circle: null as { latLng: [number, number]; options: { radius?: number } } | null,
    views: [] as { latLng: [number, number]; zoom: number }[],
    removed: 0,
  };

  const map = {
    setView: (latLng: [number, number], zoom: number) => {
      calls.views.push({ latLng, zoom });
      return map;
    },
    remove: () => {
      calls.removed++;
    },
  };
  const layer = { addTo: () => layer };
  const circle = {
    addTo: () => circle,
    setLatLng: (latLng: [number, number]) => {
      if (calls.circle) {
        calls.circle.latLng = latLng;
      }
    },
  };

  const leaflet = {
    map: (_element: HTMLElement, options: unknown) => {
      calls.mapOptions = options;
      return map;
    },
    tileLayer: (url: string, options: { attribution?: string }) => {
      calls.tileUrl = url;
      calls.tileOptions = options;
      return layer;
    },
    circle: (latLng: [number, number], options: { radius?: number }) => {
      calls.circle = { latLng, options };
      return circle;
    },
  };

  return { leaflet, calls };
}

@Component({
  selector: 'app-location-map-host',
  imports: [LocationMap],
  template: `@if (visible()) {<app-location-map [latitude]="latitude()" [longitude]="longitude()" />}`,
})
class Host {
  visible = signal(true);
  latitude = signal(19.433);
  longitude = signal(-99.133);
}

describe('LocationMap', () => {
  let fixture: ComponentFixture<Host>;

  const create = async (loader: () => Promise<unknown>): Promise<void> => {
    TestBed.configureTestingModule({ providers: [{ provide: LEAFLET_LOADER, useValue: loader }] });
    fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  it('draws the approximate zone as a circle centred on the coordinates, with the OpenStreetMap credit', async () => {
    const { leaflet, calls } = createFakeLeaflet();
    await create(() => Promise.resolve(leaflet));

    expect(calls.tileUrl).toContain('tile.openstreetmap.org');
    expect(calls.tileOptions?.attribution).toContain('OpenStreetMap');
    expect(calls.circle?.latLng).toEqual([19.433, -99.133]);
    expect(calls.circle?.options.radius).toBe(AREA_RADIUS_METERS);
    expect(calls.views.at(-1)).toEqual({ latLng: [19.433, -99.133], zoom: MAP_ZOOM });
  });

  it('does not hijack the mouse wheel, so the page keeps scrolling', async () => {
    const { leaflet, calls } = createFakeLeaflet();
    await create(() => Promise.resolve(leaflet));

    expect(calls.mapOptions).toMatchObject({ scrollWheelZoom: false });
  });

  it('is exposed to assistive technology as a labelled region', async () => {
    await create(() => new Promise(() => undefined));

    const region = fixture.nativeElement.querySelector('[role="region"]') as HTMLElement;
    expect(region.getAttribute('aria-label')).toContain('ubicación aproximada');
  });

  it('moves the map when the coordinates change', async () => {
    const { leaflet, calls } = createFakeLeaflet();
    await create(() => Promise.resolve(leaflet));

    fixture.componentInstance.latitude.set(25.67);
    fixture.componentInstance.longitude.set(-100.31);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(calls.views.at(-1)).toEqual({ latLng: [25.67, -100.31], zoom: MAP_ZOOM });
    expect(calls.circle?.latLng).toEqual([25.67, -100.31]);
  });

  it('releases the map when the component is destroyed', async () => {
    const { leaflet, calls } = createFakeLeaflet();
    await create(() => Promise.resolve(leaflet));

    fixture.componentInstance.visible.set(false);
    fixture.detectChanges();

    expect(calls.removed).toBe(1);
  });

  it('does not build a map if the component was destroyed while Leaflet was loading', async () => {
    const { leaflet, calls } = createFakeLeaflet();
    let release: (value: unknown) => void = () => undefined;
    await create(() => new Promise((resolve) => (release = resolve)));

    fixture.destroy();
    release(leaflet);
    await Promise.resolve();

    expect(calls.circle).toBeNull();
  });

  it('tells the visitor when Leaflet cannot be loaded', async () => {
    await create(() => Promise.reject(new Error('offline')));

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('No se pudo cargar el mapa');
  });
});