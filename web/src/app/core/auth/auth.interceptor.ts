import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/**
 * Agrega el JWT solo a las peticiones hacia la API propia (nunca a terceros) y cierra la sesión
 * cuando la API responde 401, salvo en el login, donde el 401 significa credenciales inválidas.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.token();

  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  const isLogin = req.url === `${environment.apiUrl}/auth/login`;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin && auth.isAuthenticated()) {
        auth.logout(router.url);
      }
      return throwError(() => error);
    }),
  );
};
