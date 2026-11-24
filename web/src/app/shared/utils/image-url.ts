import { environment } from '../../../environments/environment';

/** Dirección pública de una imagen de propiedad (la API la entrega sin sesión). */
export function imageUrl(imageId: string): string {
  return `${environment.apiUrl}/images/${imageId}`;
}
