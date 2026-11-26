import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Sugerencias para el formulario de propiedades, tomadas del catálogo de ubicaciones de la API. Son solo ayuda: el usuario puede
 * escribir un nombre que no exista y la API lo da de alta al guardar. Requieren sesión.
 */
@Injectable({ providedIn: 'root' })
export class LocationsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/locations`;

  /** Los estados de México y los que se hayan dado de alta, por orden alfabético. */
  states(): Observable<string[]> {
    return this.http.get<string[]>(`${this.url}/states`);
  }

  /** Ciudades que contienen el texto, del estado indicado (nombre o abreviatura) o de todos si está vacío. */
  cities(state: string, text: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.url}/cities`, { params: this.params({ state, q: text }) });
  }

  /** Colonias de la ciudad que contienen el texto. Sin estado o ciudad no hay sugerencias (la API responde vacío). */
  neighborhoods(state: string, city: string, text: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.url}/neighborhoods`, { params: this.params({ state, city, q: text }) });
  }

  /** Solo envía los parámetros con texto (un parámetro vacío no filtra, pero ensucia la dirección). */
  private params(values: Record<string, string>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(values)) {
      if (value.trim()) {
        params = params.set(key, value.trim());
      }
    }
    return params;
  }
}
