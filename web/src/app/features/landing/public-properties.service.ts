import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { CurrencyCode, OperationType, PropertyType } from '../properties/property.models';

/** Lo que la API publica a un visitante: sin calle, código postal ni estado de la propiedad. */
export interface PublicProperty {
  id: string;
  title: string;
  description: string | null;
  type: PropertyType;
  operation: OperationType;
  price: number;
  currency: CurrencyCode;
  neighborhood: string | null;
  city: string;
  state: string;
  bedrooms: number | null;
  bathrooms: number | null;
  constructionArea: number | null;
  landArea: number | null;
  /** Imágenes en orden de galería; la primera es la portada. */
  imageIds: string[];
  agencyName: string | null;
}

/** Filtros de la portada. Cadena vacía significa «sin filtro». */
export interface PublicPropertyFilters {
  operation: OperationType | '';
  location: string;
  type: PropertyType | '';
}

export const NO_FILTERS: PublicPropertyFilters = { operation: '', location: '', type: '' };

@Injectable({ providedIn: 'root' })
export class PublicPropertiesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/public/properties`;

  /** Detalle de una propiedad publicada. Responde 404 si no existe o ya no está publicada. */
  getById(id: string): Observable<PublicProperty> {
    return this.http.get<PublicProperty>(`${this.url}/${id}`);
  }

  getPaged(filters: PublicPropertyFilters, page: number, pageSize: number): Observable<PagedResult<PublicProperty>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (filters.operation) {
      params = params.set('operation', filters.operation);
    }
    if (filters.type) {
      params = params.set('type', filters.type);
    }
    if (filters.location.trim()) {
      params = params.set('location', filters.location.trim());
    }
    return this.http.get<PagedResult<PublicProperty>>(this.url, { params });
  }
}
