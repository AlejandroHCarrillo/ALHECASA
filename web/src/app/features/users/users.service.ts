import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../../shared/models/paged-result';
import { Agency, CreateUserRequest, UpdateUserRequest, User } from './user.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);
  private readonly usersUrl = `${environment.apiUrl}/users`;
  private readonly agenciesUrl = `${environment.apiUrl}/agencies`;

  getPaged(search: string, page: number, pageSize: number): Observable<PagedResult<User>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    return this.http.get<PagedResult<User>>(this.usersUrl, { params });
  }

  create(request: CreateUserRequest): Observable<User> {
    return this.http.post<User>(this.usersUrl, request);
  }

  update(id: string, request: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.usersUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.usersUrl}/${id}`);
  }

  getAgencies(): Observable<Agency[]> {
    return this.http.get<Agency[]>(this.agenciesUrl);
  }
}
