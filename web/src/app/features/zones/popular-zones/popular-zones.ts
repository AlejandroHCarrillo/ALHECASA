import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { ZoneLink, mainOperation, zoneCommands } from '../zone.models';
import { ZonesService } from '../zones.service';

const MAX_ZONES = 18;

/**
 * «Explora por zona»: enlaces a las ciudades y colonias con más propiedades publicadas. Además de ayudar a navegar, son los enlaces que
 * siguen los buscadores desde la portada hasta las páginas por zona. Si no hay zonas (o no cargan), no muestra nada.
 */
@Component({
  selector: 'app-popular-zones',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './popular-zones.html',
})
export class PopularZones implements OnInit {
  private readonly zonesService = inject(ZonesService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly zones = signal<ZoneLink[]>([]);

  ngOnInit(): void {
    this.zonesService
      .top(MAX_ZONES)
      .pipe(
        catchError(() => of([] as ZoneLink[])),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((zones) => this.zones.set(zones));
  }

  protected commands(zone: ZoneLink): string[] {
    return zoneCommands(mainOperation(zone), zone.citySlug, zone.neighborhoodSlug);
  }

  protected label(zone: ZoneLink): string {
    return zone.neighborhoodSlug ? `${zone.name}, ${zone.cityName}` : zone.name;
  }

  protected count(zone: ZoneLink): number {
    return zone.saleCount + zone.rentCount;
  }
}
