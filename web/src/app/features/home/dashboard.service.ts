import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AgencyDashboard } from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  /** Resumen de la inmobiliaria del usuario (personal de una inmobiliaria o particular). */
  get(): Observable<AgencyDashboard> {
    return this.http.get<AgencyDashboard>(`${environment.apiUrl}/dashboard`);
  }
}