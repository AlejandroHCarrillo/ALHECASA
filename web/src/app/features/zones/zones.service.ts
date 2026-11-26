import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Zone, ZoneLink } from './zone.models';

/** Zonas públicas (ciudades y colonias con propiedades publicadas). No requieren sesión. */
@Injectable({ providedIn: 'root' })
export class ZonesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/public/zones`;

  /** Una ciudad o una colonia. Responde 404 si no existe o no tiene ninguna propiedad publicada. */
  get(citySlug: string, neighborhoodSlug?: string): Observable<Zone> {
    const parts = neighborhoodSlug ? [citySlug, neighborhoodSlug] : [citySlug];
    return this.http.get<Zone>(`${this.url}/${parts.map(encodeURIComponent).join('/')}`);
  }

  /** Las zonas con más propiedades publicadas, ciudades y colonias mezcladas. */
  top(limit: number): Observable<ZoneLink[]> {
    return this.http.get<ZoneLink[]>(this.url, { params: new HttpParams().set('limit', limit) });
  }
}
