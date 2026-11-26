import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AgencyProfile, UpdateAgencyProfileRequest } from './agency-profile.models';

@Injectable({ providedIn: 'root' })
export class AgencyProfileService {
  private readonly http = inject(HttpClient);

  /** El perfil de la propia inmobiliaria (solo su administrador). */
  getMine(): Observable<AgencyProfile> {
    return this.http.get<AgencyProfile>(`${environment.apiUrl}/agencies/me`);
  }

  update(request: UpdateAgencyProfileRequest): Observable<AgencyProfile> {
    return this.http.put<AgencyProfile>(`${environment.apiUrl}/agencies/me`, request);
  }

  setLogo(file: File): Observable<AgencyProfile> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.put<AgencyProfile>(`${environment.apiUrl}/agencies/me/logo`, form);
  }

  removeLogo(): Observable<AgencyProfile> {
    return this.http.delete<AgencyProfile>(`${environment.apiUrl}/agencies/me/logo`);
  }

  /** Lo que ve un visitante de una inmobiliaria (sin sesión). 404 si no existe, está inactiva o es un particular. */
  getPublic(agencyId: string): Observable<AgencyProfile> {
    return this.http.get<AgencyProfile>(`${environment.apiUrl}/public/agencies/${agencyId}`);
  }
}