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
  /** Solo las publicaciones con «me gusta». Es un filtro de la portada: no viaja a la API como tal (ver `ids` en `getPaged`). */
  favoritesOnly: boolean;
}

export const NO_FILTERS: PublicPropertyFilters = { operation: '', location: '', type: '', favoritesOnly: false };

@Injectable({ providedIn: 'root' })
export class PublicPropertiesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/public/properties`;

  /** Detalle de una propiedad publicada. Responde 404 si no existe o ya no está publicada. */
  getById(id: string): Observable<PublicProperty> {
    return this.http.get<PublicProperty>(`${this.url}/${id}`);
  }

  /**
   * @param ids Si se indica, la API solo devuelve esas publicaciones (las que sigan publicadas). Un arreglo vacío
   *   no se envía, y entonces la API no filtra por identificador: quien llama debe evitar pedir «favoritas» sin ninguna.
   */
  getPaged(
    filters: PublicPropertyFilters,
    page: number,
    pageSize: number,
    ids: readonly string[] | null = null,
  ): Observable<PagedResult<PublicProperty>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const id of ids ?? []) {
      params = params.append('ids', id);
    }
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
