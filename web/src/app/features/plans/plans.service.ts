import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Plan, SavePlanRequest } from './plan.models';

@Injectable({ providedIn: 'root' })
export class PlansService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/plans`;

  list(): Observable<Plan[]> {
    return this.http.get<Plan[]>(this.url);
  }

  create(request: SavePlanRequest): Observable<Plan> {
    return this.http.post<Plan>(this.url, request);
  }

  update(id: string, request: SavePlanRequest): Observable<Plan> {
    return this.http.put<Plan>(`${this.url}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
