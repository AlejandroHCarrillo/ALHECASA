import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { PublicProperty } from '../landing/public-properties.service';
import { Agency } from '../users/user.models';
import { AdvancedCriteria, NO_ADVANCED_CRITERIA, appendAdvancedParams } from './property-criteria';
import { CreatePropertyRequest, OperationType, Property, PropertyStatus, PropertyType, UpdatePropertyRequest } from './property.models';

/** Filtros de la lista del personal: búsqueda de texto, estado, tipo y operación, más los avanzados. Cadena vacía, 
ull o alse significan «sin filtro». */
export interface PropertyListFilters extends AdvancedCriteria {
  search: string;
  status: PropertyStatus | '';
  type: PropertyType | '';
  operation: OperationType | '';
}

export const NO_LIST_FILTERS: PropertyListFilters = { search: '', status: '', type: '', operation: '', ...NO_ADVANCED_CRITERIA };

@Injectable({ providedIn: 'root' })
export class PropertiesService {
  private readonly http = inject(HttpClient);
  private readonly propertiesUrl = `${environment.apiUrl}/properties`;
  private readonly agenciesUrl = `${environment.apiUrl}/agencies`;

  getPaged(filters: PropertyListFilters, page: number, pageSize: number): Observable<PagedResult<Property>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }
    if (filters.status) {
      params = params.set('status', filters.status);
    }
    if (filters.type) {
      params = params.set('type', filters.type);
    }
    if (filters.operation) {
      params = params.set('operation', filters.operation);
    }
    return this.http.get<PagedResult<Property>>(this.propertiesUrl, { params: appendAdvancedParams(params, filters) });
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

  /** Destaca o quita el destacado de una propiedad publicada; usa un lugar del paquete de destacados de la cuenta. */
  setFeatured(id: string, featured: boolean): Observable<Property> {
    return this.http.put<Property>(`${this.propertiesUrl}/${id}/featured`, { featured });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.propertiesUrl}/${id}`);
  }

  /** Solo la usa el administrador de plataforma, que elige la inmobiliaria al crear. */
  getAgencies(): Observable<Agency[]> {
    return this.http.get<Agency[]>(this.agenciesUrl);
  }
}
