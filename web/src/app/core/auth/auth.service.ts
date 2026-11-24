import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserRole } from '../models/user-role';
import { CurrentUser, LoginRequest, LoginResponse, Session } from './auth.models';

const SESSION_KEY = 'alhecasa.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _session = signal<Session | null>(this.readStoredSession());

  readonly session = this._session.asReadonly();
  readonly user = computed<CurrentUser | null>(() => this._session()?.user ?? null);
  readonly token = computed(() => this._session()?.token ?? null);
  readonly isAuthenticated = computed(() => this._session() !== null);

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap((response) => {
        const session: Session = {
          token: response.accessToken,
          expiresAt: response.expiresAt,
          user: response.user,
        };
        localStorage.setItem(SESSION_KEY, JSON.stringify(session));
        this._session.set(session);
      }),
    );
  }

  /** El usuario cierra su sesión a propósito: vuelve a la portada pública. */
  logout(): void {
    this.clearSession();
    void this.router.navigateByUrl('/');
  }

  /**
   * La API rechazó el token (venció o dejó de ser válido): se cierra la sesión y se pide ingresar de nuevo,
   * con `returnUrl` para regresar a la pantalla en la que estaba.
   */
  expireSession(returnUrl: string): void {
    this.clearSession();
    void this.router.navigate(['/login'], { queryParams: { returnUrl } });
  }

  private clearSession(): void {
    localStorage.removeItem(SESSION_KEY);
    this._session.set(null);
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.user()?.role;
    return role !== undefined && roles.includes(role);
  }

  private readStoredSession(): Session | null {
    try {
      const raw = localStorage.getItem(SESSION_KEY);
      if (!raw) {
        return null;
      }

      const session = JSON.parse(raw) as Session;
      const valid = !!session.token && !!session.user && new Date(session.expiresAt).getTime() > Date.now();
      if (!valid) {
        localStorage.removeItem(SESSION_KEY);
        return null;
      }

      return session;
    } catch {
      localStorage.removeItem(SESSION_KEY);
      return null;
    }
  }
}
