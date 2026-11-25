import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { PublicProperty } from '../landing/public-properties.service';
import { Agency } from '../users/user.models';
import { CreatePropertyRequest, Property, UpdatePropertyRequest } from './property.models';

@Injectable({ providedIn: 'root' })
export class PropertiesService {
  private readonly http = inject(HttpClient);
  private readonly propertiesUrl = `${environment.apiUrl}/properties`;
  private readonly agenciesUrl = `${environment.apiUrl}/agencies`;

  getPaged(search: string, page: number, pageSize: number): Observable<PagedResult<Property>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResult<Property>>(this.propertiesUrl, { params });
  }

  /** Propiedades publicadas de la inmobiliaria del usuario, con el contenido público (lo que ve un visitante). */
  getGallery(page: number, pageSize: number): Observable<PagedResult<PublicProperty>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<PublicProperty>>(`${this.propertiesUrl}/gallery`, { params });
  }

  create(request: CreatePropertyRequest): Observable<Property> {
    return this.http.post<Property>(this.propertiesUrl, request);
  }

  update(id: string, request: UpdatePropertyRequest): Observable<Property> {
    return this.http.put<Property>(`${this.propertiesUrl}/${id}`, request);
  }

  /** Cambia la galería en una sola operación: agrega `files` y quita `removeImageIds`. */
  uploadImages(id: string, files: File[], removeImageIds: string[]): Observable<Property> {
    const body = new FormData();
    for (const file of files) {
      body.append('files', file, file.name);
    }
    for (const imageId of removeImageIds) {
      body.append('removeImageIds', imageId);
    }
    return this.http.post<Property>(`${this.propertiesUrl}/${id}/images`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.propertiesUrl}/${id}`);
  }

  /** Solo la usa el administrador de plataforma, que elige la inmobiliaria al crear. */
  getAgencies(): Observable<Agency[]> {
    return this.http.get<Agency[]>(this.agenciesUrl);
  }
}
