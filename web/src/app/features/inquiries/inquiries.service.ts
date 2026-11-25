import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { AddFollowUpRequest, Inquiry, InquiryStatus } from './inquiry.models';

/** Bandeja de solicitudes de informes del personal de la inmobiliaria. */
@Injectable({ providedIn: 'root' })
export class InquiriesService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/inquiries`;

  getPaged(status: InquiryStatus | '', search: string, page: number, pageSize: number): Observable<PagedResult<Inquiry>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResult<Inquiry>>(this.url, { params });
  }

  addFollowUp(id: string, request: AddFollowUpRequest): Observable<Inquiry> {
    return this.http.post<Inquiry>(`${this.url}/${id}/follow-ups`, request);
  }

  changeStatus(id: string, status: InquiryStatus): Observable<Inquiry> {
    return this.http.put<Inquiry>(`${this.url}/${id}/status`, { status });
  }
}